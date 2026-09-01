using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Quick Cast Profile", fileName = "EarthQuickCastProfile")]
    public sealed class EarthQuickCastProfile : ScriptableObject
    {
        public const float MaximumProjectileSpeed = 150f;

        [SerializeField, Range(0.2f, 0.7f)] private float doubleClickSeconds = 0.42f;
        [SerializeField, Range(10f, 60f)] private float minimumLaunchSpeed = 30f;
        [SerializeField, Range(10f, 60f)] private float maximumLaunchSpeed = 38f;
        [SerializeField, Range(0.25f, 3.5f)] private float launchForceMultiplier = 2.5f;
        [SerializeField, Range(0.1f, 0.35f)] private float primeAmount01 = 0.18f;
        [SerializeField, Range(0.08f, 0.25f)] private float extractionSeconds = 0.15f;

        public EarthQuickCastProfileData Data
        {
            get
            {
                float minimum = Mathf.Clamp(
                    minimumLaunchSpeed * launchForceMultiplier,
                    1f,
                    MaximumProjectileSpeed);
                float maximum = Mathf.Clamp(
                    maximumLaunchSpeed * launchForceMultiplier,
                    minimum,
                    MaximumProjectileSpeed);
                return new EarthQuickCastProfileData(
                    doubleClickSeconds,
                    minimum,
                    maximum,
                    extractionSeconds);
            }
        }
        public float PrimeAmount01 => primeAmount01;
        public float ExtractionSeconds => extractionSeconds;
        public float LaunchForceMultiplier => launchForceMultiplier;
    }
}
