using System;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [Serializable]
    public struct CapsuleShadowProxyBinding
    {
        [SerializeField] private Transform start;
        [SerializeField] private Transform end;
        [SerializeField] private Vector3 startOffsetLocal;
        [SerializeField] private Vector3 endOffsetLocal;
        [SerializeField, Min(0.001f)] private float radius;
        [SerializeField, Min(0.001f)] private float softness;

        public CapsuleShadowProxyBinding(
            Transform start,
            Transform end,
            Vector3 startOffsetLocal,
            Vector3 endOffsetLocal,
            float radius,
            float softness)
        {
            this.start = start;
            this.end = end;
            this.startOffsetLocal = startOffsetLocal;
            this.endOffsetLocal = endOffsetLocal;
            this.radius = Mathf.Max(0.001f, radius);
            this.softness = Mathf.Max(0.001f, softness);
        }

        public bool TryResolve(out CapsuleShadowProxy proxy)
        {
            proxy = default;
            if (start == null)
                return false;
            Transform resolvedEnd = end != null ? end : start;
            Vector3 startWorld = start.TransformPoint(startOffsetLocal);
            Vector3 endWorld = resolvedEnd.TransformPoint(endOffsetLocal);
            float scale = Mathf.Max(MaxAbsScale(start.lossyScale), MaxAbsScale(resolvedEnd.lossyScale));
            proxy = new CapsuleShadowProxy(
                startWorld,
                endWorld,
                radius * scale,
                softness * scale);
            return proxy.IsValid;
        }

        private static float MaxAbsScale(Vector3 scale)
        {
            return Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }
    }

    [DisallowMultipleComponent]
    public sealed class CapsuleShadowCaster : MonoBehaviour
    {
        public const int MaximumProxiesPerCaster = 4;

        [Header("Static authoring (group 0 is intentionally unbound)")]
        [SerializeField] private uint stableGroupId = 0u;
        [SerializeField] private uint generation = 0u;
        [SerializeField] private CapsuleShadowCasterClass classification =
            CapsuleShadowCasterClass.Other;
        [SerializeField] private CapsuleShadowProxyBinding[] proxies =
            Array.Empty<CapsuleShadowProxyBinding>();

        private CapsuleShadowRegistrationHandle _handle =
            CapsuleShadowRegistrationHandle.Invalid;
        private uint _runtimeStableGroupId;
        private uint _runtimeGeneration;
        private CapsuleShadowCasterClass _runtimeClassification;
        private bool _hasRuntimeBinding;

        public uint StableGroupId => _hasRuntimeBinding
            ? _runtimeStableGroupId
            : stableGroupId;
        public uint Generation => _hasRuntimeBinding
            ? _runtimeGeneration
            : generation;
        public CapsuleShadowCasterClass Classification => _hasRuntimeBinding
            ? _runtimeClassification
            : classification;
        public bool HasValidBinding => StableGroupId != 0u;
        public bool HasRuntimeBinding => _hasRuntimeBinding;
        public int ProxyCount => proxies != null
            ? Mathf.Min(proxies.Length, MaximumProxiesPerCaster)
            : 0;
        public bool IsRegistered => CapsuleShadowBuffer.Shared.IsRegistrationCurrent(_handle);
        public bool IsActiveGeneration => CapsuleShadowBuffer.Shared.IsGenerationActive(_handle);

        private void OnEnable()
        {
            if (HasValidBinding)
                RegisterCurrentBinding();
        }

        private void OnDisable()
        {
            Unregister();
            ClearRuntimeBinding();
        }

        public bool Bind(
            uint groupId,
            uint currentGeneration,
            CapsuleShadowCasterClass currentClassification)
        {
            Unregister();
            ClearRuntimeBinding();
            if (groupId == 0u)
            {
                Debug.LogError(
                    $"{nameof(CapsuleShadowCaster)} on '{name}' rejected stable group ID 0.",
                    this);
                return false;
            }

            _runtimeStableGroupId = groupId;
            _runtimeGeneration = currentGeneration;
            _runtimeClassification = currentClassification;
            _hasRuntimeBinding = true;
            return !isActiveAndEnabled || RegisterCurrentBinding();
        }

        public bool Rebind(
            uint groupId,
            uint currentGeneration,
            CapsuleShadowCasterClass currentClassification)
        {
            return Bind(groupId, currentGeneration, currentClassification);
        }

        public void Unbind()
        {
            Unregister();
            ClearRuntimeBinding();
        }

        public bool ConfigureProxies(CapsuleShadowProxyBinding[] bindings)
        {
            if (bindings == null ||
                bindings.Length == 0 ||
                bindings.Length > MaximumProxiesPerCaster)
            {
                Debug.LogError(
                    $"{nameof(CapsuleShadowCaster)} requires 1-{MaximumProxiesPerCaster} proxies.",
                    this);
                return false;
            }

            bool wasRegistered = IsRegistered;
            if (wasRegistered)
                Unregister();
            proxies = bindings;
            return !wasRegistered || RegisterCurrentBinding();
        }

        public bool TryGetProxy(int index, out CapsuleShadowProxy proxy)
        {
            proxy = default;
            return proxies != null &&
                index >= 0 &&
                index < ProxyCount &&
                proxies[index].TryResolve(out proxy);
        }

        public float EstimateWorldDiameter()
        {
            float diameter = 0f;
            for (int index = 0; index < ProxyCount; index++)
            {
                if (TryGetProxy(index, out CapsuleShadowProxy proxy))
                    diameter = Mathf.Max(diameter, proxy.WorldDiameter);
            }
            return diameter;
        }

        public static bool CommitGeneration(uint groupId, uint nextGeneration)
        {
            return CapsuleShadowBuffer.Shared.TryCommitGeneration(groupId, nextGeneration);
        }

        public static bool ReleaseGroup(uint groupId, uint committedGeneration)
        {
            return CapsuleShadowBuffer.Shared.TryReleaseGroup(groupId, committedGeneration);
        }

        private bool RegisterCurrentBinding()
        {
            if (!HasValidBinding || ProxyCount == 0)
            {
                Debug.LogError(
                    $"{nameof(CapsuleShadowCaster)} on '{name}' has no valid identity or proxy.",
                    this);
                return false;
            }

            var record = new CapsuleShadowCasterRecord(
                this,
                StableGroupId,
                Generation,
                Classification);
            if (CapsuleShadowBuffer.Shared.TryRegister(record, out _handle))
                return true;

            Debug.LogError(
                $"Capsule-shadow buffer rejected '{name}'. Check stable identity and fixed capacity.",
                this);
            return false;
        }

        private void Unregister()
        {
            CapsuleShadowBuffer.Shared.Unregister(_handle);
            _handle = CapsuleShadowRegistrationHandle.Invalid;
        }

        private void ClearRuntimeBinding()
        {
            _runtimeStableGroupId = 0u;
            _runtimeGeneration = 0u;
            _runtimeClassification = CapsuleShadowCasterClass.Other;
            _hasRuntimeBinding = false;
        }
    }
}
