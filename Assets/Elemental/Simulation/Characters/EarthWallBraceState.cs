using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>Presentation admission only; never changes motor or climbing authority.</summary>
    public struct EarthWallBraceState
    {
        public float ContactSeconds;
        public float Weight;

        public void Step(bool permitted, bool reachableContact, float forwardIntent,
            float relativeForwardSpeed, float deltaSeconds)
        {
            float dt = math.clamp(deltaSeconds, 0f, .1f);
            bool blocked = permitted && reachableContact && forwardIntent > .25f &&
                           math.abs(relativeForwardSpeed) < .65f;
            ContactSeconds = blocked ? ContactSeconds + dt : 0f;
            float target = ContactSeconds >= .12f ? 1f : 0f;
            float step = dt / (target > Weight ? .15f : .12f);
            Weight = math.clamp(Weight + math.clamp(target - Weight, -step, step), 0f, 1f);
        }
    }
}
