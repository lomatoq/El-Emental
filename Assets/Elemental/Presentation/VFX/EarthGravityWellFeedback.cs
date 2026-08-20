using Elemental.Presentation.Camera;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DisallowMultipleComponent]
    public sealed class EarthGravityWellFeedback : MonoBehaviour
    {
        [SerializeField] private MagicExecutor executor;
        [SerializeField] private Transform ringRoot;
        [SerializeField] private LineRenderer[] rings;
        [SerializeField] private ParticleSystem motes;
        [SerializeField] private Light focusLight;
        [SerializeField] private PlanetCameraRig cameraRig;
        [SerializeField] private Transform planetCenter;
        private float _nextMote;
        private float _nextCameraPulse;
        private uint _sequence;

        public void Configure(
            MagicExecutor configuredExecutor,
            Transform configuredRingRoot,
            LineRenderer[] configuredRings,
            ParticleSystem configuredMotes,
            Light configuredLight,
            PlanetCameraRig configuredCameraRig,
            Transform configuredPlanetCenter)
        {
            executor = configuredExecutor;
            ringRoot = configuredRingRoot;
            rings = configuredRings;
            motes = configuredMotes;
            focusLight = configuredLight;
            cameraRig = configuredCameraRig;
            planetCenter = configuredPlanetCenter;
            SetVisible(false);
        }

        private void Update()
        {
            bool active = executor != null && executor.IsGravityWellActive;
            SetVisible(active);
            if (!active) return;
            Vector3 focus = executor.GravityWellFocus;
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            Vector3 up = focus - center;
            up = up.sqrMagnitude > 0.01f ? up.normalized : Vector3.up;
            ringRoot.SetPositionAndRotation(focus, Quaternion.FromToRotation(Vector3.up, up));
            EarthGravityStructureIntent intent = executor.GravityStructureIntent;
            float strength = intent == EarthGravityStructureIntent.Neutral
                ? executor.GravityWellStrength
                : executor.GravityStructurePhase;
            float rotationSign = intent == EarthGravityStructureIntent.Disassemble ? -1f : 1f;
            Color gestureColor = intent == EarthGravityStructureIntent.Repair
                ? new Color(0.28f, 1f, 0.72f, 0.92f)
                : intent == EarthGravityStructureIntent.Disassemble
                    ? new Color(1f, 0.30f, 0.18f, 0.92f)
                    : new Color(0.78f, 0.58f, 0.24f, 0.88f);
            for (int index = 0; index < rings.Length; index++)
            {
                float phase = Time.unscaledTime * (1.6f + (index * 0.31f)) + index * 1.9f;
                float radius = Mathf.Lerp(0.48f + (index * 0.34f),
                    1.05f + (index * 0.62f), strength) * (1f + Mathf.Sin(phase) * 0.055f);
                rings[index].transform.localScale = Vector3.one * radius;
                rings[index].transform.localRotation = Quaternion.Euler(
                    0f, phase * Mathf.Rad2Deg * rotationSign, 0f);
                rings[index].startColor = gestureColor;
                rings[index].endColor = new Color(
                    gestureColor.r, gestureColor.g, gestureColor.b, gestureColor.a * 0.18f);
            }
            if (focusLight != null)
            {
                focusLight.transform.position = focus + (up * 0.3f);
                focusLight.intensity = Mathf.Lerp(0.35f, 2.4f, strength) *
                                       (0.88f + Mathf.Sin(Time.unscaledTime * 13f) * 0.12f);
            }
            if (Time.unscaledTime >= _nextMote)
            {
                _nextMote = Time.unscaledTime + Mathf.Lerp(0.085f, 0.032f, strength);
                EmitMote(focus, up, strength);
            }
            if (strength > 0.55f && Time.unscaledTime >= _nextCameraPulse)
            {
                _nextCameraPulse = Time.unscaledTime + 0.12f;
                cameraRig?.AddPresentationImpulse(
                    Mathf.Lerp(0.008f, 0.025f, strength), 0.1f, ++_sequence ^ 0x47524156u);
            }
        }

        private void EmitMote(Vector3 focus, Vector3 up, float strength)
        {
            if (motes == null) return;
            float angle = (++_sequence * 2.399963f) + Time.unscaledTime * 1.7f;
            Vector3 tangent = Vector3.Cross(up, Mathf.Abs(Vector3.Dot(up, Vector3.forward)) < 0.9f
                ? Vector3.forward : Vector3.right).normalized;
            Vector3 bitangent = Vector3.Cross(up, tangent).normalized;
            Vector3 radial = (tangent * Mathf.Cos(angle)) + (bitangent * Mathf.Sin(angle));
            float radius = Mathf.Lerp(executor.GravityWellRadius * 0.72f, 1.2f, strength);
            var emit = new ParticleSystem.EmitParams
            {
                position = focus + (radial * radius) + (up * Mathf.Sin(angle * 1.7f) * 0.35f),
                velocity = (-radial * Mathf.Lerp(3.2f, 7.5f, strength)) +
                           (Vector3.Cross(up, radial) * 2.1f),
                startSize = Mathf.Lerp(0.10f, 0.22f, strength),
                startLifetime = Mathf.Lerp(0.55f, 0.92f, strength)
            };
            motes.Emit(emit, 1);
        }

        private void SetVisible(bool visible)
        {
            if (ringRoot != null && ringRoot.gameObject.activeSelf != visible)
                ringRoot.gameObject.SetActive(visible);
            if (!visible && focusLight != null) focusLight.intensity = 0f;
        }
    }
}
