using Elemental.Runtime.Characters;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Matter;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DisallowMultipleComponent]
    public sealed class EarthStoneCounterPresenter : MonoBehaviour
    {
        [SerializeField] private EarthStoneCounterGuard guard;
        [SerializeField] private EarthCharacterPoseController pose;
        private EarthStoneCounterGuard _subscribed;
        public uint PresentedSequence { get; private set; }
        public void Configure(EarthStoneCounterGuard configuredGuard, EarthCharacterPoseController configuredPose)
        { Unsubscribe(); guard = configuredGuard; pose = configuredPose; if (isActiveAndEnabled) Subscribe(); }
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void Subscribe()
        {
            if (guard == null || pose == null || _subscribed == guard) return;
            _subscribed = guard; _subscribed.Countered += OnCountered;
        }
        private void Unsubscribe()
        { if (_subscribed != null) _subscribed.Countered -= OnCountered; _subscribed = null; }
        private void OnCountered(EarthStoneCounterImpact impact)
        {
            pose.RequestSemanticPresentation(EarthTechniqueKind.Grip, EarthTechniqueId.QuickStonePunch,
                impact.Sequence, impact.Point, impact.SourceMass, impact.ClosingSpeed / .08f,
                immediateActionBoundary: true);
            PresentedSequence = impact.Sequence;
        }
    }
}
