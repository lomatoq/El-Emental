using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    [DisallowMultipleComponent]
    public sealed class HumanoidRagdollBridge : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private ActiveRagdollPuppet puppet;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.01f)] private float recoveryBlendSeconds = 0.28f;

        private Vector3 _defaultLocalPosition;
        private Quaternion _defaultLocalRotation;
        private float _blend = 1f;
        private bool _subscribed;

        public void Configure(Animator configuredAnimator, ActiveRagdollPuppet configuredPuppet, Transform configuredVisualRoot)
        {
            Unsubscribe();
            animator = configuredAnimator;
            puppet = configuredPuppet;
            visualRoot = configuredVisualRoot;
            CaptureDefault();
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake() => CaptureDefault();

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || puppet == null) return;
            puppet.StateChanged += HandleState;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (puppet != null) puppet.StateChanged -= HandleState;
            _subscribed = false;
        }

        private void LateUpdate()
        {
            if (visualRoot == null || _blend >= 1f) return;
            _blend = Mathf.Min(1f, _blend + Time.deltaTime / Mathf.Max(0.01f, recoveryBlendSeconds));
            float eased = _blend * _blend * (3f - 2f * _blend);
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, _defaultLocalPosition, eased);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, _defaultLocalRotation, eased);
        }

        private void HandleState(CharacterPhysicalState state)
        {
            if (animator == null) return;
            if (state.Mode == CharacterPhysicalMode.FullRagdoll)
            {
                animator.enabled = false;
                _blend = 0f;
            }
            else if (!animator.enabled)
            {
                animator.enabled = true;
                // Do not Rebind: the presentation's rigid mesh parts are parented
                // to Humanoid bones after instantiation. Rebuilding bindings here
                // advances Animator state time but can freeze those bone poses.
                animator.Play("Locomotion", 0, 0f);
                animator.Update(0f);
                _blend = 0f;
            }
        }

        private void CaptureDefault()
        {
            if (visualRoot == null) return;
            _defaultLocalPosition = visualRoot.localPosition;
            _defaultLocalRotation = visualRoot.localRotation;
        }
    }
}
