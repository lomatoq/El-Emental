using System.Collections.Generic;
using Elemental.Input.Gestures;
using Elemental.Runtime.World;
using Elemental.Simulation.Magic;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthMagicPoseDriver : MonoBehaviour
    {
        [SerializeField] private MagicInputController input;
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;

        private Quaternion _rootBase;
        private Quaternion _leftBase;
        private Quaternion _rightBase;
        private float _accent;
        private bool _previewing;
        private bool _subscribed;

        public void Configure(
            MagicInputController configuredInput,
            MagicExecutor configuredExecutor,
            Transform configuredVisualRoot,
            Transform configuredLeftArm,
            Transform configuredRightArm)
        {
            if (_subscribed) Unsubscribe();
            input = configuredInput;
            executor = configuredExecutor;
            visualRoot = configuredVisualRoot;
            leftArm = configuredLeftArm;
            rightArm = configuredRightArm;
            _rootBase = visualRoot.localRotation;
            _leftBase = leftArm.localRotation;
            _rightBase = rightArm.localRotation;
            if (isActiveAndEnabled && (executor != null || input != null)) Subscribe();
        }

        private void OnEnable()
        {
            if ((executor != null || input != null) && !_subscribed) Subscribe();
        }

        private void OnDisable()
        {
            if (_subscribed) Unsubscribe();
        }

        private void Update()
        {
            if (visualRoot == null || leftArm == null || rightArm == null) return;
            bool holding = executor != null && executor.HeldBody != null;
            AbilityId ability = input != null ? input.SelectedAbility : default;
            float pose = holding ? 1f : _previewing ? 0.58f : Mathf.Clamp01(_accent);
            _accent = Mathf.MoveTowards(_accent, 0f, Time.deltaTime * 2.6f);

            float rootLean = ability == EarthAbilityIds.FlickThrow ? 10f : holding ? -7f : -3f;
            float armForward = ability == EarthAbilityIds.FlickThrow ? 72f : holding ? 48f : 24f;
            float armSpread = holding ? 62f : _previewing ? 38f : 20f;
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation,
                _rootBase * Quaternion.Euler(rootLean * pose, 0f, 0f), Time.deltaTime * 9f);
            leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation,
                _leftBase * Quaternion.Euler(armForward * pose, 0f, armSpread * pose), Time.deltaTime * 12f);
            rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation,
                _rightBase * Quaternion.Euler(armForward * pose, 0f, -armSpread * pose), Time.deltaTime * 12f);
        }

        private void OnPulled(FragmentSpawnedEvent _) => _accent = 1f;
        private void OnLaunched(FragmentLaunchedEvent _) => _accent = 1.25f;
        private void OnBodyGrabbed(EarthBodyGrabbedEvent _) => _accent = 1f;
        private void OnBodyReleased(EarthBodyReleasedEvent _) => _accent = 1.25f;
        private void OnWallRaised(WallRaisedEvent _) => _accent = 0.7f;
        private void OnPreviewChanged(IReadOnlyList<Vector3> _) => _previewing = true;
        private void OnPreviewCleared() => _previewing = false;

        private void Subscribe()
        {
            if (executor != null)
            {
                executor.Events.FragmentSpawned += OnPulled;
                executor.Events.FragmentLaunched += OnLaunched;
                executor.Events.EarthBodyGrabbed += OnBodyGrabbed;
                executor.Events.EarthBodyReleased += OnBodyReleased;
                executor.Events.WallRaised += OnWallRaised;
            }
            if (input != null)
            {
                input.PreviewChanged += OnPreviewChanged;
                input.PreviewCleared += OnPreviewCleared;
            }
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (executor != null)
            {
                executor.Events.FragmentSpawned -= OnPulled;
                executor.Events.FragmentLaunched -= OnLaunched;
                executor.Events.EarthBodyGrabbed -= OnBodyGrabbed;
                executor.Events.EarthBodyReleased -= OnBodyReleased;
                executor.Events.WallRaised -= OnWallRaised;
            }
            if (input != null)
            {
                input.PreviewChanged -= OnPreviewChanged;
                input.PreviewCleared -= OnPreviewCleared;
            }
            _subscribed = false;
        }
    }
}
