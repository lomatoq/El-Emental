using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Quick Cast Profile", fileName = "EarthQuickCastProfile")]
    public sealed class EarthQuickCastProfile : ScriptableObject
    {
        [SerializeField, Range(0.2f, 0.7f)] private float doubleClickSeconds = 0.42f;
        [SerializeField, Range(20f, 40f)] private float minimumLaunchSpeed = 30f;
        [SerializeField, Range(20f, 45f)] private float maximumLaunchSpeed = 38f;
        [SerializeField, Range(0.1f, 0.35f)] private float primeAmount01 = 0.18f;
        [SerializeField, Range(0.08f, 0.25f)] private float extractionSeconds = 0.15f;

        public EarthQuickCastProfileData Data => new EarthQuickCastProfileData(
            doubleClickSeconds, minimumLaunchSpeed, maximumLaunchSpeed, extractionSeconds);
        public float PrimeAmount01 => primeAmount01;
        public float ExtractionSeconds => extractionSeconds;
    }
}
