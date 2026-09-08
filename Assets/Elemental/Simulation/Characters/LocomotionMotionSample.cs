using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    public readonly struct LocomotionMotionSample
    {
        public readonly uint Tick, SupportId;
        public readonly float3 RelativeVelocity, DesiredVelocity, Up, Facing;
        public readonly float Phase01, Distance, Pulse;
        public readonly bool Grounded;
        public float Speed => math.length(RelativeVelocity);
        public LocomotionMotionSample(uint tick, uint supportId, float3 velocity, float3 desired,
            float3 up, float3 facing, float phase, float distance, float pulse, bool grounded)
        {
            Tick = tick; SupportId = supportId; RelativeVelocity = velocity; DesiredVelocity = desired;
            Up = up; Facing = facing; Phase01 = phase; Distance = distance; Pulse = pulse; Grounded = grounded;
        }
    }

    public readonly struct LocomotionRhythmSample
    {
        public readonly float PlaybackRate, StrideScale;
        public LocomotionRhythmSample(float rate, float stride) { PlaybackRate = rate; StrideScale = stride; }
    }

    public static class LocomotionRhythmSolver
    {
        public static float3 SupportTractionAcceleration(float3 velocity, float3 desiredVelocity,
            float3 supportUp, float3 gravityAcceleration, float maximumAcceleration, float deltaTime)
        {
            float3 up = math.normalizesafe(supportUp, new float3(0f, 1f, 0f));
            float3 tangent = velocity - up * math.dot(velocity, up);
            float3 desired = desiredVelocity - up * math.dot(desiredVelocity, up);
            // Grounded legs counter gravity along their support, within the same
            // traction budget. Ground contact still owns normal gravity/adhesion.
            float3 gravityTangent = gravityAcceleration - up * math.dot(gravityAcceleration, up);
            float3 acceleration = (desired - tangent) / math.max(.0001f, deltaTime) - gravityTangent;
            float length = math.length(acceleration);
            return length > maximumAcceleration && length > .0001f
                ? acceleration * (math.max(0f, maximumAcceleration) / length) : acceleration;
        }

        public static float Pulse(float phase, bool allowed) => allowed && math.isfinite(phase)
            ? 1f + .05f * math.sin(phase * (4f * math.PI)) : 1f;
        public static float AdvancePhase(float phase, float distance, float cycleDistance) =>
            math.frac((math.isfinite(phase) ? phase : 0f) +
                math.max(0f, math.isfinite(distance) ? distance : 0f) / math.max(.1f, cycleDistance));
        public static LocomotionRhythmSample Resolve(float speed, float authoredSpeed, bool allowed)
        {
            if (!allowed || !math.isfinite(speed) || speed < .12f ||
                !math.isfinite(authoredSpeed) || authoredSpeed < .12f)
                return new LocomotionRhythmSample(1f, 1f);
            float rate = math.clamp(speed / authoredSpeed, .8f, 1.25f);
            return new LocomotionRhythmSample(rate, math.clamp(speed / (authoredSpeed * rate), .9f, 1.1f));
        }
        public static float ContactPhase(float frame, float previousPlant, float nextPlant)
        {
            if (nextPlant - previousPlant <= .001f) return 0f;
            return math.saturate((frame - previousPlant) / (nextPlant - previousPlant));
        }
        public static void FillContactPhases(bool[] contacts, float[] phases)
        {
            int count = contacts.Length;
            if (count == 0 || phases.Length != count) return;
            int first = -1;
            for (int i = 0; i < count; i++)
                if (contacts[i] && !contacts[(i + count - 1) % count]) { first = i; break; }
            if (first < 0) { System.Array.Clear(phases, 0, count); return; }
            int previous = first;
            for (int step = 1; step <= count; step++)
            {
                int index = (first + step) % count;
                if (step != count && (!contacts[index] || contacts[(index + count - 1) % count])) continue;
                int next = first + step;
                for (int frame = previous; frame < next; frame++)
                    phases[frame % count] = ContactPhase(frame, previous, next);
                previous = next;
            }
        }
    }
}
