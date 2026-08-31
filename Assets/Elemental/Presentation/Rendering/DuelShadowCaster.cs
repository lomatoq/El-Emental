using System;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class DuelShadowCaster : MonoBehaviour
    {
        [Header("Static authoring (group 0 is intentionally unbound)")]
        [SerializeField] private uint stableGroupId = 0u;
        [SerializeField] private uint generation = 0u;
        [SerializeField] private DuelShadowCasterClass classification =
            DuelShadowCasterClass.Other;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        private DuelShadowRegistrationHandle[] _handles;
        private uint _runtimeStableGroupId;
        private uint _runtimeGeneration;
        private DuelShadowCasterClass _runtimeClassification;
        private bool _hasRuntimeBinding;

        public uint StableGroupId => _hasRuntimeBinding
            ? _runtimeStableGroupId
            : stableGroupId;
        public uint Generation => _hasRuntimeBinding
            ? _runtimeGeneration
            : generation;
        public DuelShadowCasterClass Classification => _hasRuntimeBinding
            ? _runtimeClassification
            : classification;
        public bool HasValidBinding => StableGroupId != 0u;
        public bool HasRuntimeBinding => _hasRuntimeBinding;
        public int RegisteredRendererCount => CountRegistrations(false);
        public int ActiveRegistrationCount => CountRegistrations(true);

        private void Awake()
        {
            CacheRenderersIfNeeded();
        }

        private void OnEnable()
        {
            CacheRenderersIfNeeded();
            if (HasValidBinding)
                RegisterCurrentBinding();
        }

        private void OnDisable()
        {
            UnregisterRenderers();
            ClearRuntimeBinding();
        }

        /// <summary>
        /// Binds a runtime-owned caster identity. Rebinding is idempotent: every
        /// previous handle is invalidated before the new identity can register.
        /// Runtime bindings are cleared on disable so a pooled object cannot
        /// register its prior acquisition before its producer binds it again.
        /// </summary>
        public bool Bind(
            uint groupId,
            uint currentGeneration,
            DuelShadowCasterClass currentClassification)
        {
            UnregisterRenderers();
            ClearRuntimeBinding();
            if (groupId == 0u)
            {
                Debug.LogError(
                    $"{nameof(DuelShadowCaster)} on '{name}' rejected stable group ID 0.",
                    this);
                return false;
            }

            _runtimeStableGroupId = groupId;
            _runtimeGeneration = currentGeneration;
            _runtimeClassification = currentClassification;
            _hasRuntimeBinding = true;
            CacheRenderersIfNeeded();
            return !isActiveAndEnabled || RegisterCurrentBinding();
        }

        public bool Rebind(
            uint groupId,
            uint currentGeneration,
            DuelShadowCasterClass currentClassification)
        {
            return Bind(groupId, currentGeneration, currentClassification);
        }

        public void Unbind()
        {
            UnregisterRenderers();
            ClearRuntimeBinding();
        }

        public static bool CommitGeneration(uint groupId, uint nextGeneration)
        {
            return DuelShadowCasterRegistry.Shared.TryCommitGeneration(
                groupId,
                nextGeneration);
        }

        public static bool ReleaseGroup(uint groupId, uint committedGeneration)
        {
            return DuelShadowCasterRegistry.Shared.TryReleaseGroup(
                groupId,
                committedGeneration);
        }

        private void CacheRenderersIfNeeded()
        {
            if (renderers == null || renderers.Length == 0)
            {
                Renderer localRenderer = GetComponent<Renderer>();
                renderers = localRenderer != null
                    ? new[] { localRenderer }
                    : Array.Empty<Renderer>();
            }
            if (_handles == null || _handles.Length != renderers.Length)
                _handles = new DuelShadowRegistrationHandle[renderers.Length];
        }

        private bool RegisterCurrentBinding()
        {
            if (!HasValidBinding)
                return false;
            if (renderers.Length == 0)
            {
                Debug.LogError(
                    $"{nameof(DuelShadowCaster)} on '{name}' has no explicit opaque renderer.",
                    this);
                return false;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer targetRenderer = renderers[index];
                if (targetRenderer == null ||
                    !DuelShadowCasterPolicy.IsSupportedOpaqueRenderer(targetRenderer))
                {
                    string rendererName = targetRenderer != null
                        ? targetRenderer.name
                        : $"slot {index}";
                    Debug.LogError(
                        $"Duel-shadow caster '{rendererName}' is not an opaque " +
                        "MeshRenderer or SkinnedMeshRenderer and was rejected.",
                        this);
                    UnregisterRenderers();
                    return false;
                }

                var record = new DuelShadowCasterRecord(
                    targetRenderer,
                    targetRenderer.bounds,
                    StableGroupId,
                    Generation,
                    Classification,
                    ResolveSubmeshCount(targetRenderer));
                if (!DuelShadowCasterRegistry.Shared.TryRegister(
                        record,
                        out _handles[index]))
                {
                    Debug.LogError(
                        $"Duel-shadow registry rejected '{targetRenderer.name}'. " +
                        "Check the stable ID/generation and fixed registry capacity.",
                        this);
                    UnregisterRenderers();
                    return false;
                }
            }

            return true;
        }

        private void UnregisterRenderers()
        {
            if (_handles == null)
                return;
            for (int index = 0; index < _handles.Length; index++)
            {
                DuelShadowCasterRegistry.Shared.Unregister(_handles[index]);
                _handles[index] = DuelShadowRegistrationHandle.Invalid;
            }
        }

        private void ClearRuntimeBinding()
        {
            _runtimeStableGroupId = 0u;
            _runtimeGeneration = 0u;
            _runtimeClassification = DuelShadowCasterClass.Other;
            _hasRuntimeBinding = false;
        }

        private int CountRegistrations(bool activeOnly)
        {
            if (_handles == null)
                return 0;
            int count = 0;
            for (int index = 0; index < _handles.Length; index++)
            {
                DuelShadowRegistrationHandle handle = _handles[index];
                bool included = activeOnly
                    ? DuelShadowCasterRegistry.Shared.IsGenerationActive(handle)
                    : DuelShadowCasterRegistry.Shared.IsRegistrationCurrent(handle);
                if (included)
                    count++;
            }
            return count;
        }

        private static int ResolveSubmeshCount(Renderer targetRenderer)
        {
            if (targetRenderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                return skinned.sharedMesh.subMeshCount;
            if (targetRenderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                return filter.sharedMesh.subMeshCount;
            return 1;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && (renderers == null || renderers.Length == 0))
            {
                Renderer localRenderer = GetComponent<Renderer>();
                renderers = localRenderer != null
                    ? new[] { localRenderer }
                    : Array.Empty<Renderer>();
            }
        }
#endif
    }
}
