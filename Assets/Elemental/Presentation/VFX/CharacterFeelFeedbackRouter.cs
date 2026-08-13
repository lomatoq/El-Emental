using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class CharacterFeelFeedbackRouter : MonoBehaviour
    {
        [SerializeField] private ActiveRagdollPuppet puppet;
        [SerializeField] private ParticleSystem impactBurst;
        [SerializeField] private AudioSource impactAudio;
        [SerializeField] private Transform cameraTransform;
        [SerializeField, Min(0f)] private float cameraKickScale = 0.0025f;
        [SerializeField, Min(0f)] private float cameraReturnSpeed = 12f;

        private Vector3 _cameraOffset;
        private Vector3 _appliedOffset;

        public void Configure(
            ActiveRagdollPuppet configuredPuppet,
            ParticleSystem configuredImpactBurst,
            AudioSource configuredImpactAudio,
            Transform configuredCameraTransform)
        {
            if (isActiveAndEnabled && puppet != null)
            {
                Unsubscribe();
            }

            puppet = configuredPuppet;
            impactBurst = configuredImpactBurst;
            impactAudio = configuredImpactAudio;
            cameraTransform = configuredCameraTransform;
            if (isActiveAndEnabled && puppet != null)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            if (puppet != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (puppet != null)
            {
                Unsubscribe();
            }

            RemoveCameraOffset();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                return;
            }

            cameraTransform.localPosition -= _appliedOffset;
            _cameraOffset = Vector3.Lerp(
                _cameraOffset,
                Vector3.zero,
                1f - Mathf.Exp(-cameraReturnSpeed * Time.deltaTime));
            _appliedOffset = _cameraOffset;
            cameraTransform.localPosition += _appliedOffset;
        }

        private void HandleImpact(Vector3 point, float impulse)
        {
            if (impactBurst != null)
            {
                impactBurst.transform.position = point;
                int count = Mathf.Clamp(Mathf.CeilToInt(impulse * 0.08f), 2, 24);
                impactBurst.Emit(count);
            }

            if (impactAudio != null && impactAudio.clip != null)
            {
                impactAudio.volume = Mathf.Clamp01(impulse / 120f);
                impactAudio.PlayOneShot(impactAudio.clip);
            }

            Vector2 random = Random.insideUnitCircle;
            _cameraOffset += new Vector3(random.x, random.y, 0f) * Mathf.Min(impulse * cameraKickScale, 0.3f);
        }

        private void HandleState(CharacterPhysicalState state)
        {
            if (impactBurst == null)
            {
                return;
            }

            ParticleSystem.MainModule main = impactBurst.main;
            main.startColor = state.Mode == CharacterPhysicalMode.FullRagdoll
                ? new Color(1f, 0.28f, 0.16f, 1f)
                : new Color(1f, 0.78f, 0.2f, 1f);
        }

        private void Subscribe()
        {
            puppet.ImpactObserved += HandleImpact;
            puppet.StateChanged += HandleState;
        }

        private void Unsubscribe()
        {
            puppet.ImpactObserved -= HandleImpact;
            puppet.StateChanged -= HandleState;
        }

        private void RemoveCameraOffset()
        {
            if (cameraTransform != null)
            {
                cameraTransform.localPosition -= _appliedOffset;
            }

            _appliedOffset = Vector3.zero;
            _cameraOffset = Vector3.zero;
        }
    }
}
