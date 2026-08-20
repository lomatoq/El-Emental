using Elemental.Simulation.Bending;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthConstructionFrameRuntime : MonoBehaviour
    {
        public EarthConstructionFrame Frame { get; private set; }

        public void Configure(
            uint supportId,
            uint supportGeneration,
            Vector3 origin,
            Vector3 normal,
            Vector3 tangent,
            Quaternion authoredRotation,
            Quaternion supportRotation,
            ConstructionOrientationMode orientationMode)
        {
            Quaternion local = Quaternion.Inverse(supportRotation) * authoredRotation;
            Frame = new EarthConstructionFrame(
                supportId,
                supportGeneration,
                ToFloat3(origin),
                ToFloat3(normal),
                ToFloat3(tangent),
                ToQuaternion(authoredRotation),
                ToQuaternion(local),
                orientationMode);
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
        private static quaternion ToQuaternion(Quaternion value) =>
            new quaternion(value.x, value.y, value.z, value.w);
    }
}
