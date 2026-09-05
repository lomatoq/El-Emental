using Unity.Mathematics;

namespace Elemental.Simulation.Animation
{
    /// <summary>Stable tangent frame used by animation queries on arbitrary gravity.</summary>
    public readonly struct GravityMotionFrame
    {
        public readonly float3 Origin;
        public readonly float3 Right;
        public readonly float3 Up;
        public readonly float3 Forward;

        private GravityMotionFrame(float3 origin, float3 right, float3 up, float3 forward)
        {
            Origin = origin;
            Right = right;
            Up = up;
            Forward = forward;
        }

        public static GravityMotionFrame Create(float3 origin, float3 localUp, float3 facing)
        {
            float3 up = math.normalizesafe(localUp, new float3(0f, 1f, 0f));
            float3 forward = facing - up * math.dot(facing, up);
            if (math.lengthsq(forward) < 1e-6f)
            {
                float3 fallback = math.abs(up.y) < 0.95f
                    ? new float3(0f, 1f, 0f)
                    : new float3(0f, 0f, 1f);
                forward = math.cross(math.cross(up, fallback), up);
            }

            forward = math.normalize(forward);
            float3 right = math.normalize(math.cross(up, forward));
            forward = math.normalize(math.cross(right, up));
            return new GravityMotionFrame(origin, right, up, forward);
        }

        public float3 WorldPointToLocal(float3 point) => WorldDirectionToLocal(point - Origin);

        public float3 WorldDirectionToLocal(float3 direction) => new float3(
            math.dot(direction, Right),
            math.dot(direction, Up),
            math.dot(direction, Forward));

        public float3 LocalPointToWorld(float3 point) => Origin + LocalDirectionToWorld(point);

        public float3 LocalDirectionToWorld(float3 direction) =>
            Right * direction.x + Up * direction.y + Forward * direction.z;
    }
}
