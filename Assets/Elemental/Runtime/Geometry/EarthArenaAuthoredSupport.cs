using UnityEngine;

namespace Elemental.Runtime.Geometry
{
    public enum EarthArenaSupportDomain : byte
    {
        ArenaFloor = 0,
        PlanetSphere = 1
    }

    /// <summary>
    /// Serialized evidence for the exact support frame chosen by the authoring
    /// adapter. The crater floor is not radial, so runtime QA must validate the
    /// same immutable floor triangle instead of guessing a different ray frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthArenaAuthoredSupport : MonoBehaviour
    {
        [SerializeField] private EarthArenaSupportDomain domain;
        [SerializeField] private Vector3 supportUp = Vector3.up;

        public EarthArenaSupportDomain Domain => domain;
        public Vector3 SupportUp => supportUp.sqrMagnitude > 0.5f
            ? supportUp.normalized
            : Vector3.up;

        public void Configure(EarthArenaSupportDomain configuredDomain, Vector3 configuredUp)
        {
            domain = configuredDomain;
            supportUp = configuredUp.sqrMagnitude > 0.5f
                ? configuredUp.normalized
                : Vector3.up;
        }
    }
}
