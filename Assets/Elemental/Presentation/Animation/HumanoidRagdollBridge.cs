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

        public void Configure(Animator configuredAnimator, ActiveRagdollPuppet configuredPuppet, Transform configuredVisualRoot)
        {
            animator = configuredAnimator;
            puppet = configuredPuppet;
            visualRoot = configuredVisualRoot;
            CaptureDefault();
        }

        private void Awake() => CaptureDefault();

        private void OnEnable()
        {
            if (puppet != null) puppet.StateChanged += HandleState;
        }

        private void OnDisable()
        {
            if (puppet != null) puppet.StateChanged -= HandleState;
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
                animator.Rebind();
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
