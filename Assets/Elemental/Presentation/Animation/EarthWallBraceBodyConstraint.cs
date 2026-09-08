using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace Elemental.Presentation.Animation
{
    [Unity.Burst.BurstCompile]
    public struct EarthWallBraceBodyJob : IWeightedAnimationJob
    {
        public ReadWriteTransformHandle spine;
        public ReadOnlyTransformHandle leftShoulder, rightShoulder, facing;
        public FloatProperty jobWeight { get; set; }
        public void ProcessRootMotion(AnimationStream stream) { }
        public void ProcessAnimation(AnimationStream stream)
        {
            float weight = Mathf.Clamp01(jobWeight.Get(stream));
            if (weight <= 0f) { AnimationRuntimeUtils.PassThrough(stream, spine); return; }
            Quaternion basis = facing.GetRotation(stream);
            Vector3 up = basis * Vector3.up;
            Vector3 right = basis * Vector3.right;
            Vector3 shoulders = Vector3.ProjectOnPlane(rightShoulder.GetPosition(stream) - leftShoulder.GetPosition(stream), up);
            float yaw = shoulders.sqrMagnitude > .0001f
                ? Mathf.Clamp(Vector3.SignedAngle(shoulders, right, up), -30f, 30f) : 0f;
            Quaternion authored = spine.GetRotation(stream);
            // Current stream pose is the input every frame, so this cannot accumulate.
            // No bone translation, head target, or actor-root warp is introduced.
            Quaternion braced = Quaternion.AngleAxis(18f, right) * Quaternion.AngleAxis(yaw, up) * authored;
            spine.SetRotation(stream, Quaternion.Slerp(authored, braced, weight));
        }
    }

    [System.Serializable]
    public struct EarthWallBraceBodyData : IAnimationJobData
    {
        public Transform spine, leftShoulder, rightShoulder;
        [SyncSceneToStream] public Transform facing;
        public bool IsValid() => spine != null && leftShoulder != null && rightShoulder != null && facing != null;
        public void SetDefaultValues() { spine = leftShoulder = rightShoulder = facing = null; }
    }

    public sealed class EarthWallBraceBodyBinder : AnimationJobBinder<EarthWallBraceBodyJob, EarthWallBraceBodyData>
    {
        public override EarthWallBraceBodyJob Create(Animator animator, ref EarthWallBraceBodyData data, Component component) =>
            new EarthWallBraceBodyJob
            {
                spine = ReadWriteTransformHandle.Bind(animator, data.spine),
                leftShoulder = ReadOnlyTransformHandle.Bind(animator, data.leftShoulder),
                rightShoulder = ReadOnlyTransformHandle.Bind(animator, data.rightShoulder),
                facing = ReadOnlyTransformHandle.Bind(animator, data.facing)
            };
        public override void Destroy(EarthWallBraceBodyJob job) { }
    }

    [DisallowMultipleComponent]
    public sealed class EarthWallBraceBodyConstraint : RigConstraint<EarthWallBraceBodyJob, EarthWallBraceBodyData, EarthWallBraceBodyBinder> { }
}
