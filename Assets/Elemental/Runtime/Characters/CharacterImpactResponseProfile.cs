using System;
using Elemental.Simulation.Combat;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [CreateAssetMenu(menuName = "Elemental/Characters/Impact Response Profile")]
    public sealed class CharacterImpactResponseProfile : ScriptableObject
    {
        [SerializeField] private ImpactResponseMode responseMode = ImpactResponseMode.Legacy;
        [SerializeField, Min(0.1f)] private float singleStoneRootVelocity = 0.8f;
        [SerializeField, Min(0.1f)] private float maximumRagdollRise = 2f;
        [SerializeField, Min(0.1f)] private float maximumRagdollTangentSpeed = 4f;
        [Header("Calibrated source response")]
        [SerializeField] private SourceCalibration looseStone = new SourceCalibration(1f, 0.8f, 0.9f);
        [SerializeField] private SourceCalibration armorProjectile = new SourceCalibration(1.1f, 0.85f, 1.35f);
        [SerializeField] private SourceCalibration pillarWave = new SourceCalibration(1.28f, 0.48f, 2.3f);
        [SerializeField] private SourceCalibration pillarCrest = new SourceCalibration(1.32f, 0.58f, 2.4f);
        [SerializeField] private SourceCalibration stonePunch = new SourceCalibration(1.28f, 0.62f, 2f);
        [SerializeField] private SourceCalibration surfNose = new SourceCalibration(1.15f, 0.65f, 2.4f);
        [SerializeField] private SourceCalibration genericPhysics = new SourceCalibration(1f, 0.8f, 1.5f);

        [Header("Localized hit presentation")]
        [SerializeField] private bool localizedHitReaction = true;
        [SerializeField, Range(0.12f, 0.22f)] private float localizedHitDuration = 0.18f;
        [SerializeField, Range(0.45f, 0.60f)] private float localizedParentWeight = 0.55f;
        [SerializeField, Range(0.20f, 0.32f)] private float localizedTorsoWeight = 0.25f;
        [SerializeField, Range(0.10f, 0.30f)] private float localizedHeadTransferWeight = 0.18f;
        [SerializeField, Range(7f, 14f)] private float localizedArmChestMaxAngle = 12f;
        [SerializeField, Range(4f, 8f)] private float localizedHeadMaxAngle = 6f;
        [SerializeField, Range(0.05f, 0.35f)] private float localizedHipsLegWeight = 0.18f;

        public ImpactResponseMode ResponseMode => responseMode;
        public float SingleStoneRootVelocity => Mathf.Max(0.1f, singleStoneRootVelocity);
        public float MaximumRagdollRise => Mathf.Max(0.1f, maximumRagdollRise);
        public float MaximumRagdollTangentSpeed => Mathf.Max(0.1f, maximumRagdollTangentSpeed);
        public EarthCharacterImpactTuning Tuning => EarthCharacterImpactTuning.Default;
        public bool LocalizedHitReaction => localizedHitReaction;
        public float LocalizedHitDuration => Mathf.Clamp(localizedHitDuration, 0.12f, 0.22f);
        public float LocalizedParentWeight => Mathf.Clamp(localizedParentWeight, 0.45f, 0.60f);
        public float LocalizedTorsoWeight => Mathf.Clamp(localizedTorsoWeight, 0.20f, 0.32f);
        public float LocalizedHeadTransferWeight => Mathf.Clamp(localizedHeadTransferWeight, 0.10f, 0.30f);
        public float LocalizedArmChestMaxAngle => Mathf.Clamp(localizedArmChestMaxAngle, 7f, 14f);
        public float LocalizedHeadMaxAngle => Mathf.Clamp(localizedHeadMaxAngle, 4f, 8f);
        public float LocalizedHipsLegWeight => Mathf.Clamp(localizedHipsLegWeight, 0.05f, 0.35f);

        public EarthCharacterImpactCalibration CalibrationFor(EarthCharacterImpactSourceKind source)
        {
            SourceCalibration calibration = source switch
            {
                EarthCharacterImpactSourceKind.LooseStone => looseStone,
                EarthCharacterImpactSourceKind.ArmorProjectile => armorProjectile,
                EarthCharacterImpactSourceKind.BotProjectile => armorProjectile,
                EarthCharacterImpactSourceKind.PillarWave => pillarWave,
                EarthCharacterImpactSourceKind.PillarCrest => pillarCrest,
                EarthCharacterImpactSourceKind.StonePunch => stonePunch,
                EarthCharacterImpactSourceKind.SurfNose => surfNose,
                EarthCharacterImpactSourceKind.Physics => genericPhysics,
                _ => default
            };
            return calibration.ToRuntime(source);
        }

        public void ConfigureMode(ImpactResponseMode mode) => responseMode = mode;

        [Serializable]
        private struct SourceCalibration
        {
            [Min(0f)] public float reactionMultiplier;
            [Min(0f)] public float movementMultiplier;
            [Min(0.1f)] public float maximumRootVelocityChange;

            public SourceCalibration(
                float configuredReactionMultiplier,
                float configuredMovementMultiplier,
                float configuredMaximumRootVelocityChange)
            {
                reactionMultiplier = configuredReactionMultiplier;
                movementMultiplier = configuredMovementMultiplier;
                maximumRootVelocityChange = configuredMaximumRootVelocityChange;
            }

            public EarthCharacterImpactCalibration ToRuntime(EarthCharacterImpactSourceKind source)
            {
                if (maximumRootVelocityChange < 0.1f)
                    return EarthCharacterImpactCalibration.DefaultFor(source);
                return new EarthCharacterImpactCalibration(
                    reactionMultiplier,
                    movementMultiplier,
                    maximumRootVelocityChange);
            }
        }
    }
}
