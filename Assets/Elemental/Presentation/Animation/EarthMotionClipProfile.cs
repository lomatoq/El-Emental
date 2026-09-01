using System;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [Serializable]
    public sealed class EarthMotionClipProfile
    {
        [Header("Identity and provenance")]
        [SerializeField] private AnimationClip clip;
        [SerializeField] private string assetGuid = string.Empty;
        [SerializeField] private long localFileId;
        [SerializeField] private string sourceAssetPath = string.Empty;
        [SerializeField] private EarthMotionProvenance provenance;
        [SerializeField] private string provenanceLabel = string.Empty;

        [Header("Semantic and kinematic metadata")]
        [SerializeField] private EarthMotionSemanticAction semanticAction;
        [SerializeField] private EarthAuthoredActionId authoredAction;
        [SerializeField, Min(0f)] private float averageSpeedMetersPerSecond;
        [SerializeField] private Vector2 planarDirection;
        [SerializeField] private float averageYawDegreesPerSecond;
        [SerializeField] private EarthMotionStance stance;
        [SerializeField] private EarthMotionStyle style;

        [Header("Canonical continuous curves")]
        [SerializeField] private AnimationCurve leftFootContact = new AnimationCurve();
        [SerializeField] private AnimationCurve rightFootContact = new AnimationCurve();
        [SerializeField] private AnimationCurve leftFootPhase = new AnimationCurve();
        [SerializeField] private AnimationCurve rightFootPhase = new AnimationCurve();
        [SerializeField] private AnimationCurve landingContact = new AnimationCurve();
        [SerializeField] private AnimationCurve safeExit = new AnimationCurve();
        [SerializeField] private AnimationCurve pelvisCompression = new AnimationCurve();
        [SerializeField] private AnimationCurve rootEffort = new AnimationCurve();

        [Header("Authored windows")]
        [SerializeField, Range(0f, 1f)] private float landingContactPhase01;
        [SerializeField] private EarthMotionPhaseWindow safeExitWindow;
        [SerializeField] private EarthMotionPhaseWindow cancelWindow;
        [SerializeField] private EarthMotionPhaseWindow recoveryWindow;

        [Header("Occupancy, mirroring and tags")]
        [SerializeField] private EarthMotionHandOccupancy handOccupancy;
        [SerializeField] private bool supportsMirroring;
        [SerializeField] private EarthMotionEnvironmentTag environmentTags;
        [SerializeField] private EarthMotionActionTag actionTags;
        [SerializeField] private EarthMotionManualCorrection manualCorrections;

        public EarthMotionClipProfile(
            AnimationClip clip,
            string assetGuid,
            long localFileId,
            string sourceAssetPath,
            EarthMotionProvenance provenance,
            string provenanceLabel,
            EarthMotionSemanticAction semanticAction,
            EarthAuthoredActionId authoredAction,
            float averageSpeedMetersPerSecond,
            Vector2 planarDirection,
            float averageYawDegreesPerSecond,
            EarthMotionStance stance,
            EarthMotionStyle style,
            AnimationCurve[] curves,
            float landingContactPhase01,
            in EarthMotionPhaseWindow safeExitWindow,
            in EarthMotionPhaseWindow cancelWindow,
            in EarthMotionPhaseWindow recoveryWindow,
            EarthMotionHandOccupancy handOccupancy,
            bool supportsMirroring,
            EarthMotionEnvironmentTag environmentTags,
            EarthMotionActionTag actionTags)
        {
            this.clip = clip;
            this.assetGuid = assetGuid ?? string.Empty;
            this.localFileId = localFileId;
            this.sourceAssetPath = sourceAssetPath ?? string.Empty;
            this.provenance = provenance;
            this.provenanceLabel = provenanceLabel ?? string.Empty;
            this.semanticAction = semanticAction;
            this.authoredAction = authoredAction;
            this.averageSpeedMetersPerSecond = Mathf.Max(
                0f,
                float.IsFinite(averageSpeedMetersPerSecond)
                    ? averageSpeedMetersPerSecond
                    : 0f);
            this.planarDirection = IsFinite(planarDirection)
                ? Vector2.ClampMagnitude(planarDirection, 1f)
                : Vector2.zero;
            this.averageYawDegreesPerSecond = float.IsFinite(averageYawDegreesPerSecond)
                ? averageYawDegreesPerSecond
                : 0f;
            this.stance = stance;
            this.style = style;
            leftFootContact = CurveAt(curves, 0);
            rightFootContact = CurveAt(curves, 1);
            leftFootPhase = CurveAt(curves, 2);
            rightFootPhase = CurveAt(curves, 3);
            landingContact = CurveAt(curves, 4);
            safeExit = CurveAt(curves, 5);
            pelvisCompression = CurveAt(curves, 6);
            rootEffort = CurveAt(curves, 7);
            this.landingContactPhase01 = Mathf.Clamp01(
                float.IsFinite(landingContactPhase01) ? landingContactPhase01 : 0f);
            this.safeExitWindow = safeExitWindow;
            this.cancelWindow = cancelWindow;
            this.recoveryWindow = recoveryWindow;
            this.handOccupancy = handOccupancy;
            this.supportsMirroring = supportsMirroring;
            this.environmentTags = environmentTags;
            this.actionTags = actionTags;
            manualCorrections = EarthMotionManualCorrection.None;
        }

        public AnimationClip Clip => clip;
        public string AssetGuid => assetGuid;
        public long LocalFileId => localFileId;
        public string SourceAssetPath => sourceAssetPath;
        public EarthMotionProvenance Provenance => provenance;
        public string ProvenanceLabel => provenanceLabel;
        public EarthMotionSemanticAction SemanticAction => semanticAction;
        public EarthAuthoredActionId AuthoredAction => authoredAction;
        public float AverageSpeedMetersPerSecond => averageSpeedMetersPerSecond;
        public Vector2 PlanarDirection => planarDirection;
        public float AverageYawDegreesPerSecond => averageYawDegreesPerSecond;
        public EarthMotionStance Stance => stance;
        public EarthMotionStyle Style => style;
        public float LandingContactPhase01 => landingContactPhase01;
        public EarthMotionPhaseWindow SafeExitWindow => safeExitWindow;
        public EarthMotionPhaseWindow CancelWindow => cancelWindow;
        public EarthMotionPhaseWindow RecoveryWindow => recoveryWindow;
        public EarthMotionHandOccupancy HandOccupancy => handOccupancy;
        public bool SupportsMirroring => supportsMirroring;
        public EarthMotionEnvironmentTag EnvironmentTags => environmentTags;
        public EarthMotionActionTag ActionTags => actionTags;
        public EarthMotionManualCorrection ManualCorrections => manualCorrections;

        public AnimationCurve Curve(int index) => index switch
        {
            0 => leftFootContact,
            1 => rightFootContact,
            2 => leftFootPhase,
            3 => rightFootPhase,
            4 => landingContact,
            5 => safeExit,
            6 => pelvisCompression,
            7 => rootEffort,
            _ => null
        };

        public void ApplyManualCorrectionsFrom(EarthMotionClipProfile previous)
        {
            if (previous == null || previous.manualCorrections == EarthMotionManualCorrection.None)
                return;
            EarthMotionManualCorrection corrections = previous.manualCorrections;
            if ((corrections & EarthMotionManualCorrection.SemanticAction) != 0)
            {
                semanticAction = previous.semanticAction;
                authoredAction = previous.authoredAction;
            }
            if ((corrections & EarthMotionManualCorrection.Kinematics) != 0)
            {
                averageSpeedMetersPerSecond = previous.averageSpeedMetersPerSecond;
                planarDirection = previous.planarDirection;
                averageYawDegreesPerSecond = previous.averageYawDegreesPerSecond;
            }
            if ((corrections & EarthMotionManualCorrection.StanceAndStyle) != 0)
            {
                stance = previous.stance;
                style = previous.style;
            }
            if ((corrections & EarthMotionManualCorrection.ContactCurves) != 0)
            {
                leftFootContact = previous.leftFootContact;
                rightFootContact = previous.rightFootContact;
                leftFootPhase = previous.leftFootPhase;
                rightFootPhase = previous.rightFootPhase;
                landingContact = previous.landingContact;
                safeExit = previous.safeExit;
                pelvisCompression = previous.pelvisCompression;
                rootEffort = previous.rootEffort;
            }
            if ((corrections & EarthMotionManualCorrection.Windows) != 0)
            {
                landingContactPhase01 = previous.landingContactPhase01;
                safeExitWindow = previous.safeExitWindow;
                cancelWindow = previous.cancelWindow;
                recoveryWindow = previous.recoveryWindow;
            }
            if ((corrections & EarthMotionManualCorrection.HandAndMirroring) != 0)
            {
                handOccupancy = previous.handOccupancy;
                supportsMirroring = previous.supportsMirroring;
            }
            if ((corrections & EarthMotionManualCorrection.Tags) != 0)
            {
                environmentTags = previous.environmentTags;
                actionTags = previous.actionTags;
            }
            manualCorrections = corrections;
        }

        private static AnimationCurve CurveAt(AnimationCurve[] curves, int index) =>
            curves != null && index >= 0 && index < curves.Length && curves[index] != null
                ? curves[index]
                : new AnimationCurve();

        private static bool IsFinite(Vector2 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y);
    }
}
