using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Animations;

namespace Elemental.Presentation.Animation
{
    internal sealed class EarthPoseHistory : IDisposable
    {
        public EarthPoseHistory(int boneCount)
        {
            BoneHandles = new NativeArray<TransformStreamHandle>(boneCount, Allocator.Persistent);
            BoneOwnership = new NativeArray<EarthAnimationBoneOwnership>(boneCount, Allocator.Persistent);
            Initialized = new NativeArray<byte>(boneCount, Allocator.Persistent);
            PreviousTargetPositions = new NativeArray<float3>(boneCount, Allocator.Persistent);
            PreviousTargetRotations = new NativeArray<quaternion>(boneCount, Allocator.Persistent);
            PreviousOutputPositions = new NativeArray<float3>(boneCount, Allocator.Persistent);
            PreviousOutputRotations = new NativeArray<quaternion>(boneCount, Allocator.Persistent);
            OutputLinearVelocities = new NativeArray<float3>(boneCount, Allocator.Persistent);
            OutputAngularVelocities = new NativeArray<float3>(boneCount, Allocator.Persistent);
            PositionOffsets = new NativeArray<float3>(boneCount, Allocator.Persistent);
            PositionOffsetVelocities = new NativeArray<float3>(boneCount, Allocator.Persistent);
            RotationOffsets = new NativeArray<float3>(boneCount, Allocator.Persistent);
            RotationOffsetVelocities = new NativeArray<float3>(boneCount, Allocator.Persistent);
            Control = new NativeArray<EarthAnimationGraphControl>(1, Allocator.Persistent);
            Diagnostics = new NativeArray<EarthAnimationJobDiagnostics>(1, Allocator.Persistent);
        }

        public NativeArray<TransformStreamHandle> BoneHandles { get; }
        public NativeArray<EarthAnimationBoneOwnership> BoneOwnership { get; }
        public NativeArray<byte> Initialized { get; }
        public NativeArray<float3> PreviousTargetPositions { get; }
        public NativeArray<quaternion> PreviousTargetRotations { get; }
        public NativeArray<float3> PreviousOutputPositions { get; }
        public NativeArray<quaternion> PreviousOutputRotations { get; }
        public NativeArray<float3> OutputLinearVelocities { get; }
        public NativeArray<float3> OutputAngularVelocities { get; }
        public NativeArray<float3> PositionOffsets { get; }
        public NativeArray<float3> PositionOffsetVelocities { get; }
        public NativeArray<float3> RotationOffsets { get; }
        public NativeArray<float3> RotationOffsetVelocities { get; }
        public NativeArray<EarthAnimationGraphControl> Control { get; }
        public NativeArray<EarthAnimationJobDiagnostics> Diagnostics { get; }

        public int BoneCount => BoneHandles.IsCreated ? BoneHandles.Length : 0;

        public EarthInertializationJob CreateJob() => new EarthInertializationJob
        {
            BoneHandles = BoneHandles,
            BoneOwnership = BoneOwnership,
            Initialized = Initialized,
            PreviousTargetPositions = PreviousTargetPositions,
            PreviousTargetRotations = PreviousTargetRotations,
            PreviousOutputPositions = PreviousOutputPositions,
            PreviousOutputRotations = PreviousOutputRotations,
            OutputLinearVelocities = OutputLinearVelocities,
            OutputAngularVelocities = OutputAngularVelocities,
            PositionOffsets = PositionOffsets,
            PositionOffsetVelocities = PositionOffsetVelocities,
            RotationOffsets = RotationOffsets,
            RotationOffsetVelocities = RotationOffsetVelocities,
            Control = Control,
            Diagnostics = Diagnostics
        };

        public void Dispose()
        {
            Dispose(BoneHandles);
            Dispose(BoneOwnership);
            Dispose(Initialized);
            Dispose(PreviousTargetPositions);
            Dispose(PreviousTargetRotations);
            Dispose(PreviousOutputPositions);
            Dispose(PreviousOutputRotations);
            Dispose(OutputLinearVelocities);
            Dispose(OutputAngularVelocities);
            Dispose(PositionOffsets);
            Dispose(PositionOffsetVelocities);
            Dispose(RotationOffsets);
            Dispose(RotationOffsetVelocities);
            Dispose(Control);
            Dispose(Diagnostics);
        }

        private static void Dispose<T>(NativeArray<T> array) where T : struct
        {
            if (array.IsCreated) array.Dispose();
        }
    }
}
