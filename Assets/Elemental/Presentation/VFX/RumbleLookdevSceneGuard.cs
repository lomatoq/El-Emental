using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elemental.Presentation.VFX
{
    /// <summary>
    /// Scene-local ownership guard for the isolated Graphics V5 proof. The legacy
    /// Earth branch contains global rescue installers that can add camera volumes and
    /// persistent fill lights to every scene; this component prevents those systems
    /// from contaminating the approved one-sun lookdev court without changing their
    /// behaviour in the existing EarthPolishLab.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    [DisallowMultipleComponent]
    public sealed class RumbleLookdevSceneGuard : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Light keyLight;
        [SerializeField] private Volume authoredVolume;
        [SerializeField] private float scanInterval = 0.35f;

        private float _nextScanAt;

        public void Configure(Camera camera, Light light, Volume volume)
        {
            targetCamera = camera;
            keyLight = light;
            authoredVolume = volume;
            EnforceOwnership();
        }

        private void OnEnable() => EnforceOwnership();

        private void Update()
        {
            if (Time.unscaledTime < _nextScanAt) return;
            _nextScanAt = Time.unscaledTime + Mathf.Max(0.1f, scanInterval);
            EnforceOwnership();
        }

        private void EnforceOwnership()
        {
            Light[] lights = FindObjectsByType<Light>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null || light == keyLight) continue;
                light.enabled = false;
                light.intensity = 0f;
            }

            if (targetCamera != null)
            {
                MonoBehaviour[] cameraBehaviours = targetCamera.GetComponents<MonoBehaviour>();
                for (int index = 0; index < cameraBehaviours.Length; index++)
                {
                    MonoBehaviour behaviour = cameraBehaviours[index];
                    if (behaviour == null || behaviour is RumbleLensDirector) continue;
                    string typeName = behaviour.GetType().Name;
                    if (typeName.Equals("EarthChargeCameraLookdev", StringComparison.Ordinal) ||
                        typeName.Equals("EarthChargeCameraLookdevV2", StringComparison.Ordinal))
                        behaviour.enabled = false;
                }
            }

            Volume[] volumes = FindObjectsByType<Volume>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < volumes.Length; index++)
            {
                Volume candidate = volumes[index];
                if (candidate == null || candidate == authoredVolume) continue;
                string objectName = candidate.gameObject.name;
                if (objectName.StartsWith("Earth Runtime Lookdev", StringComparison.Ordinal) ||
                    objectName.StartsWith("Earth Charge", StringComparison.Ordinal))
                {
                    candidate.weight = 0f;
                    candidate.enabled = false;
                }
            }
        }
    }
}
