using System;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [Serializable]
    public struct EarthRecoveryMarkerAuthoring
    {
        [SerializeField, Range(0f, 1f)] private float feetEnablePhase;
        [SerializeField, Range(0f, 1f)] private float controlsEnablePhase;
        [SerializeField, Range(0f, 1f)] private float exitPhase;

        public EarthRecoveryMarkerAuthoring(
            float feetEnablePhase,
            float controlsEnablePhase,
            float exitPhase)
        {
            this.feetEnablePhase = feetEnablePhase;
            this.controlsEnablePhase = controlsEnablePhase;
            this.exitPhase = exitPhase;
        }

        public EarthRecoveryMarkerProfile Bake()
        {
            var markers = new EarthRecoveryMarkerProfile(
                feetEnablePhase,
                controlsEnablePhase,
                exitPhase);
            return markers.IsValid ? markers : EarthRecoveryMarkerProfile.Default;
        }
    }

    [Serializable]
    public struct EarthRecoveryPoseSampleAuthoring
    {
        [SerializeField, Min(1)] private uint clipId;
        [SerializeField] private string animatorStatePath;
        [SerializeField] private EarthRecoveryOrientation orientation;
        [SerializeField, Range(0f, 1f)] private float entryPhase;
        [SerializeField] private bool validEntry;
        [SerializeField] private Vector3 pelvisOffsetLocal;
        [SerializeField] private Vector3 chestOffset;
        [SerializeField] private Vector3 leftHandOffset;
        [SerializeField] private Vector3 rightHandOffset;
        [SerializeField] private Vector3 leftFootOffset;
        [SerializeField] private Vector3 rightFootOffset;
        [SerializeField] private Vector3 chestOutward;
        [SerializeField] private EarthRecoveryMarkerAuthoring markers;

        public EarthRecoveryPoseSampleAuthoring(
            uint clipId,
            string animatorStatePath,
            EarthRecoveryOrientation orientation,
            float entryPhase,
            Vector3 pelvisOffsetLocal,
            Vector3 chestOffset,
            Vector3 leftHandOffset,
            Vector3 rightHandOffset,
            Vector3 leftFootOffset,
            Vector3 rightFootOffset,
            Vector3 chestOutward,
            in EarthRecoveryMarkerAuthoring markers)
        {
            this.clipId = clipId;
            this.animatorStatePath = animatorStatePath;
            this.orientation = orientation;
            this.entryPhase = entryPhase;
            validEntry = true;
            this.pelvisOffsetLocal = pelvisOffsetLocal;
            this.chestOffset = chestOffset;
            this.leftHandOffset = leftHandOffset;
            this.rightHandOffset = rightHandOffset;
            this.leftFootOffset = leftFootOffset;
            this.rightFootOffset = rightFootOffset;
            this.chestOutward = chestOutward;
            this.markers = markers;
        }

        public bool TryBake(out EarthRecoveryPoseCandidate candidate)
        {
            int stateHash = string.IsNullOrWhiteSpace(animatorStatePath)
                ? 0
                : Animator.StringToHash(animatorStatePath);
            var feature = new EarthRecoveryPoseFeature(
                ToFloat3(chestOffset),
                ToFloat3(leftHandOffset),
                ToFloat3(rightHandOffset),
                ToFloat3(leftFootOffset),
                ToFloat3(rightFootOffset),
                ToFloat3(chestOutward));
            EarthRecoveryMarkerProfile bakedMarkers = markers.Bake();
            candidate = new EarthRecoveryPoseCandidate(
                clipId,
                stateHash,
                orientation,
                entryPhase,
                in feature,
                ToFloat3(pelvisOffsetLocal),
                in bakedMarkers,
                validEntry);
            return candidate.IsUsable;
        }

        private static float3 ToFloat3(Vector3 value) =>
            new float3(value.x, value.y, value.z);
    }

    [CreateAssetMenu(
        menuName = "Elemental/Character/Earth Physical Animation Profile",
        fileName = "EarthPhysicalAnimationProfile")]
    public sealed class EarthPhysicalAnimationProfile : ScriptableObject
    {
        [Header("Feature Gates")]
        [SerializeField] private bool usePoseMatchedRecovery;

        [Header("Pose Matching")]
        [SerializeField, Min(0f)] private float chestWeight = 1.4f;
        [SerializeField, Min(0f)] private float handWeight = 0.75f;
        [SerializeField, Min(0f)] private float footWeight = 1f;
        [SerializeField, Min(0f)] private float chestOutwardWeight = 1.25f;
        [SerializeField] private EarthRecoveryPoseSampleAuthoring[] recoverySamples =
            Array.Empty<EarthRecoveryPoseSampleAuthoring>();

        [Header("Recovery Safety")]
        [SerializeField, Min(0.1f)] private float supportProbeDistance = 1.4f;

        private EarthRecoveryPoseDatabase _database;
        private bool _databaseUsable;

        public bool UsePoseMatchedRecovery => usePoseMatchedRecovery;
        public float SupportProbeDistance => Mathf.Max(0.1f, supportProbeDistance);
        public EarthRecoveryPoseMatchWeights MatchWeights =>
            new EarthRecoveryPoseMatchWeights(
                chestWeight,
                handWeight,
                footWeight,
                chestOutwardWeight);

        public void ConfigureRecovery(
            bool enabled,
            EarthRecoveryPoseSampleAuthoring[] samples,
            float configuredSupportProbeDistance = 1.4f)
        {
            usePoseMatchedRecovery = enabled;
            recoverySamples = samples ?? Array.Empty<EarthRecoveryPoseSampleAuthoring>();
            supportProbeDistance = Mathf.Max(0.1f, configuredSupportProbeDistance);
            _database = null;
            _databaseUsable = false;
        }

        public bool TryGetRecoveryDatabase(out EarthRecoveryPoseDatabase database)
        {
            if (_database == null) BuildDatabase();
            database = _database;
            return _databaseUsable;
        }

        private void BuildDatabase()
        {
            int count = recoverySamples?.Length ?? 0;
            var candidates = new EarthRecoveryPoseCandidate[count];
            _databaseUsable = false;
            for (int index = 0; index < count; index++)
                _databaseUsable |= recoverySamples[index].TryBake(out candidates[index]);
            _database = new EarthRecoveryPoseDatabase(candidates);
        }

        private void OnValidate()
        {
            _database = null;
            _databaseUsable = false;
        }
    }
}
