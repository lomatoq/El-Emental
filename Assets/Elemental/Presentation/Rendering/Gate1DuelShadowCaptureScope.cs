using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    /// <summary>
    /// Installs only transient components needed for a real shadow-map/debug-
    /// receiver capture. The clean scene and renderer profile are never mutated.
    /// </summary>
    public sealed class Gate1DuelShadowCaptureScope : IDisposable
    {
        private const uint FirstCaptureGroupId = 0xE1E10001u;
        private const uint CaptureGeneration = 1u;

        private readonly List<DuelShadowCaster> _casters =
            new List<DuelShadowCaster>(32);
        private readonly List<uint> _groupIds = new List<uint>(32);
        private DuelShadowCaptureOverride.Token _overrideToken;
        private GameObject _providerObject;
        private bool _disposed;

        private Gate1DuelShadowCaptureScope()
        {
        }

        public int BoundCasterCount => _casters.Count;

        public static bool TryBegin(
            Light directionalLight,
            Transform player,
            Transform opponent,
            IReadOnlyList<Renderer> playerRenderers,
            IReadOnlyList<Renderer> opponentRenderers,
            out Gate1DuelShadowCaptureScope scope,
            out string failure)
        {
            scope = null;
            if (DuelShadowBoundsProvider.Active != null)
            {
                failure = "The scene already has a duel-shadow bounds owner; transient capture refused to replace it.";
                return false;
            }
            if (directionalLight == null || directionalLight.type != LightType.Directional ||
                player == null || opponent == null)
            {
                failure = "Transient duel shadows require a directional light and two explicit duelists.";
                return false;
            }

            var candidate = new Gate1DuelShadowCaptureScope();
            try
            {
                DuelShadowRuntimeSettings settings = CreateCaptureSettings();
                if (!DuelShadowCaptureOverride.TryBegin(
                        in settings,
                        out candidate._overrideToken,
                        out failure))
                    return false;

                candidate._providerObject = new GameObject("Gate1 Transient Duel Shadow Provider")
                {
                    hideFlags = HideFlags.DontSave
                };
                Vector3 up = player.up + opponent.up;
                if (up.sqrMagnitude < 0.25f) up = player.up;
                up.Normalize();
                candidate._providerObject.transform.position =
                    (player.position + opponent.position) * 0.5f;
                candidate._providerObject.transform.rotation =
                    Quaternion.FromToRotation(Vector3.up, up);
                DuelShadowBoundsProvider provider =
                    candidate._providerObject.AddComponent<DuelShadowBoundsProvider>();
                float radius = Mathf.Max(
                    8f,
                    Vector3.Distance(player.position, opponent.position) * 0.5f + 4f);
                if (!provider.ConfigureRuntime(
                        directionalLight,
                        player,
                        opponent,
                        candidate._providerObject.transform,
                        1.5f,
                        radius,
                        6f))
                {
                    failure = "The transient duel-shadow bounds provider rejected its explicit inputs.";
                    candidate.Dispose();
                    return false;
                }

                if (!candidate.BindRenderers(
                        playerRenderers,
                        DuelShadowCasterClass.Player,
                        ref failure) ||
                    !candidate.BindRenderers(
                        opponentRenderers,
                        DuelShadowCasterClass.Opponent,
                        ref failure))
                {
                    candidate.Dispose();
                    return false;
                }
                if (candidate.BoundCasterCount == 0)
                {
                    failure = "No eligible opaque duel renderer was available for the shadow-map capture.";
                    candidate.Dispose();
                    return false;
                }

                scope = candidate;
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                candidate.Dispose();
                failure = $"Transient duel-shadow wiring failed: {exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int index = 0; index < _casters.Count; index++)
            {
                DuelShadowCaster caster = _casters[index];
                uint groupId = _groupIds[index];
                if (caster != null)
                {
                    caster.Unbind();
                    UnityEngine.Object.Destroy(caster);
                }
                DuelShadowCaster.ReleaseGroup(groupId, CaptureGeneration);
            }
            _casters.Clear();
            _groupIds.Clear();
            if (_providerObject != null)
            {
                _providerObject.SetActive(false);
                UnityEngine.Object.Destroy(_providerObject);
                _providerObject = null;
            }
            _overrideToken.Dispose();
        }

        private bool BindRenderers(
            IReadOnlyList<Renderer> renderers,
            DuelShadowCasterClass classification,
            ref string failure)
        {
            if (renderers == null) return true;
            for (int index = 0; index < renderers.Count; index++)
            {
                Renderer renderer = renderers[index];
                if (!IsEligibleOpaqueRenderer(renderer)) continue;
                if (renderer.GetComponent<DuelShadowCaster>() != null)
                {
                    failure = $"Renderer '{renderer.name}' already has a duel-shadow caster; transient capture refused to take its identity.";
                    return false;
                }
                uint groupId = FirstCaptureGroupId + (uint)_casters.Count;
                if (groupId == 0u)
                {
                    failure = "Transient duel-shadow group identity overflowed.";
                    return false;
                }
                DuelShadowCaster caster = renderer.gameObject.AddComponent<DuelShadowCaster>();
                if (!caster.Bind(groupId, CaptureGeneration, classification))
                {
                    UnityEngine.Object.Destroy(caster);
                    failure = $"Transient duel-shadow caster '{renderer.name}' could not bind.";
                    return false;
                }
                _casters.Add(caster);
                _groupIds.Add(groupId);
            }
            return true;
        }

        private static bool IsEligibleOpaqueRenderer(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy ||
                !DuelShadowCasterPolicy.IsSupportedOpaqueRenderer(renderer))
                return false;
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0) return false;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null || material.renderQueue > 2500) return false;
            }
            return true;
        }

        private static DuelShadowRuntimeSettings CreateCaptureSettings()
        {
            return new DuelShadowRuntimeSettings(
                DuelShadowQuality.Resolve(DuelShadowQualityTier.Balanced),
                new DuelShadowClassificationSettings(0.45f, 0.8f),
                new DuelShadowStabilizationSettings(
                    12f,
                    160f,
                    1.5f,
                    4f,
                    0.5f,
                    1f,
                    0.2f,
                    1f,
                    1.5f),
                64,
                0.88f,
                0.8f,
                1.8f,
                DuelShadowDebugView.ShadowOnly);
        }
    }
}
