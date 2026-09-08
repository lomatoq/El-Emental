using Elemental.Simulation.Bending;
using UnityEngine;
namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Earth/Landing Slam", fileName = "EarthLandingSlamProfile")]
    public sealed class EarthLandingSlamProfile : ScriptableObject
    {
        [SerializeField] private EarthLandingSlamSettings settings = EarthLandingSlamSettings.Default;
        public EarthLandingSlamSettings Settings => settings.IsValid ? settings : EarthLandingSlamSettings.Default;
    }
}
