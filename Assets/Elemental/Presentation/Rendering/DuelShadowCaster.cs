using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [DisallowMultipleComponent]
    public sealed class DuelShadowCaster : MonoBehaviour
    {
        [SerializeField, Min(1)] private int stableGroupId = 1;
        [SerializeField, Min(0)] private int generation = 0;
        [SerializeField] private DuelShadowCasterClass classification =
            DuelShadowCasterClass.Other;
        [SerializeField] private Renderer[] renderers;

        private DuelShadowRegistrationHandle[] _handles;

        public int StableGroupId => stableGroupId;
        public int Generation => generation;
        public DuelShadowCasterClass Classification => classification;

        private void Awake()
        {
            CacheRenderersIfNeeded();
        }

        private void OnEnable()
        {
            CacheRenderersIfNeeded();
            RegisterRenderers();
        }

        private void OnDisable()
        {
            UnregisterRenderers();
        }

        public static bool CommitGeneration(int groupId, int nextGeneration)
        {
            return DuelShadowCasterRegistry.Shared.TryCommitGeneration(
                groupId,
                nextGeneration);
        }

        private void CacheRenderersIfNeeded()
        {
            if (renderers == null || renderers.Length == 0)
            {
                Renderer localRenderer = GetComponent<Renderer>();
                renderers = localRenderer != null
                    ? new[] { localRenderer }
                    : System.Array.Empty<Renderer>();
            }
            if (_handles == null || _handles.Length != renderers.Length)
                _handles = new DuelShadowRegistrationHandle[renderers.Length];
        }

        private void RegisterRenderers()
        {
            if (stableGroupId <= 0)
            {
                Debug.LogError(
                    $"{nameof(DuelShadowCaster)} on '{name}' requires a positive stable group ID.",
                    this);
                return;
            }

            if (renderers.Length == 0)
            {
                Debug.LogError(
                    $"{nameof(DuelShadowCaster)} on '{name}' has no explicit opaque renderer.",
                    this);
                return;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer targetRenderer = renderers[index];
                if (targetRenderer == null)
                    continue;
                if (!DuelShadowCasterPolicy.IsSupportedOpaqueRenderer(targetRenderer))
                {
                    Debug.LogError(
                        $"Duel-shadow caster '{targetRenderer.name}' is not an opaque " +
                        "MeshRenderer or SkinnedMeshRenderer and was rejected.",
                        this);
                    continue;
                }
                int submeshCount = ResolveSubmeshCount(targetRenderer);
                DuelShadowCasterRecord record = new DuelShadowCasterRecord(
                    targetRenderer,
                    targetRenderer.bounds,
                    stableGroupId,
                    generation,
                    classification,
                    submeshCount);
                if (!DuelShadowCasterRegistry.Shared.TryRegister(record, out _handles[index]))
                {
                    Debug.LogError(
                        $"Duel-shadow registry rejected '{targetRenderer.name}'. " +
                        "Check the stable ID/generation and fixed registry capacity.",
                        this);
                }
            }
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
            stableGroupId = Mathf.Max(1, stableGroupId);
            generation = Mathf.Max(0, generation);
            if (!Application.isPlaying && (renderers == null || renderers.Length == 0))
            {
                Renderer localRenderer = GetComponent<Renderer>();
                renderers = localRenderer != null
                    ? new[] { localRenderer }
                    : System.Array.Empty<Renderer>();
            }
        }
#endif
    }
}
