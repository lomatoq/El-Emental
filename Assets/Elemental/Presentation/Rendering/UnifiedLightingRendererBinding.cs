using System;
using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    [Serializable]
    public struct UnifiedLightingSlotBinding
    {
        [SerializeField, Min(0)] private int materialIndex;
        [SerializeField] private UnifiedLightingMaterialRole role;

        public UnifiedLightingSlotBinding(
            int materialIndex,
            UnifiedLightingMaterialRole role)
        {
            this.materialIndex = Mathf.Max(0, materialIndex);
            this.role = role;
        }

        public int MaterialIndex => materialIndex;
        public UnifiedLightingMaterialRole Role => role;
    }

    /// <summary>
    /// Serialized scene seam for the unified-lighting material contract. It owns
    /// no material replacement: authoring assigns the exact materials first, then
    /// this component restores the projection/family property block after scene
    /// load and every explicit representation activation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnifiedLightingRendererBinding : MonoBehaviour
    {
        [SerializeField] private UnifiedLightingMigrationProfile migrationProfile;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Vector3 planetCenterWorld;
        [SerializeField] private Matrix4x4 capturedLocalToStructure = Matrix4x4.identity;
        [SerializeField] private UnifiedLightingSlotBinding[] slots =
            Array.Empty<UnifiedLightingSlotBinding>();

        private UnifiedLightingMaterialBinder _binder;

        public int SlotCount => slots?.Length ?? 0;

        public void Configure(
            UnifiedLightingMigrationProfile profile,
            Renderer renderer,
            Vector3 planetCenter,
            Matrix4x4 localToStructure,
            UnifiedLightingSlotBinding[] configuredSlots)
        {
            migrationProfile = profile;
            targetRenderer = renderer;
            planetCenterWorld = planetCenter;
            capturedLocalToStructure = localToStructure;
            slots = configuredSlots != null
                ? (UnifiedLightingSlotBinding[])configuredSlots.Clone()
                : Array.Empty<UnifiedLightingSlotBinding>();
            EnsureBinder();
            Apply();
        }

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            EnsureBinder();
        }

        private void OnEnable() => Apply();

        public bool Apply()
        {
            if (migrationProfile == null || targetRenderer == null || SlotCount == 0)
                return false;
            EnsureBinder();
            if (_binder == null) return false;

            bool success = true;
            for (int index = 0; index < slots.Length; index++)
            {
                UnifiedLightingSlotBinding slot = slots[index];
                UnifiedLightingRoleContract contract =
                    UnifiedLightingRoleContract.Resolve(slot.Role);
                var frame = new UnifiedLightingProjectionFrame(
                    contract.ProjectionMode,
                    planetCenterWorld,
                    capturedLocalToStructure);
                success &= _binder.Bind(
                    targetRenderer,
                    slot.MaterialIndex,
                    slot.Role,
                    in frame);
            }
            return success;
        }

        private void EnsureBinder()
        {
            if (_binder == null) _binder = GetComponent<UnifiedLightingMaterialBinder>();
            if (_binder == null) _binder = gameObject.AddComponent<UnifiedLightingMaterialBinder>();
            _binder.Configure(migrationProfile);
        }
    }
}
