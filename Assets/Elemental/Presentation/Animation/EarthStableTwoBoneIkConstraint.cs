using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;

namespace Elemental.Presentation.Animation
{
    [Unity.Burst.BurstCompile]
    public struct EarthStableTwoBoneIkJob : IWeightedAnimationJob
    {
        public ReadWriteTransformHandle root;
        public ReadWriteTransformHandle mid;
        public ReadWriteTransformHandle tip;
        public ReadOnlyTransformHandle target;
        public ReadOnlyTransformHandle hint;
        public Quaternion targetRotationOffset;
        public FloatProperty targetRotationWeight;
        public FloatProperty jobWeight { get; set; }
        public float maximumReachFraction;

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            float weight = Mathf.Clamp01(jobWeight.Get(stream));
            if (weight <= 0f)
            {
                AnimationRuntimeUtils.PassThrough(stream, root);
                AnimationRuntimeUtils.PassThrough(stream, mid);
                AnimationRuntimeUtils.PassThrough(stream, tip);
                return;
            }

            Vector3 rootPosition = root.GetPosition(stream);
            Vector3 midPosition = mid.GetPosition(stream);
            Vector3 tipPosition = tip.GetPosition(stream);
            Quaternion sourceRootRotation = root.GetRotation(stream);
            Quaternion sourceMidRotation = mid.GetRotation(stream);
            Quaternion sourceTipRotation = tip.GetRotation(stream);
            target.GetGlobalTR(stream, out Vector3 requestedTarget, out Quaternion targetRotation);
            Vector3 pole = hint.IsValid(stream)
                ? hint.GetPosition(stream)
                : midPosition + Vector3.up;

            EarthStableArmIkSample sample = EarthStableArmIkGeometry.Resolve(
                rootPosition,
                requestedTarget,
                pole,
                Vector3.Distance(rootPosition, midPosition),
                Vector3.Distance(midPosition, tipPosition),
                maximumReachFraction);

            Quaternion solvedRootRotation = QuaternionExt.FromToRotation(
                midPosition - rootPosition,
                sample.Elbow - rootPosition) * sourceRootRotation;
            root.SetRotation(stream, solvedRootRotation);

            Vector3 solvedMidPosition = mid.GetPosition(stream);
            Vector3 rotatedTipPosition = tip.GetPosition(stream);
            Quaternion midAfterRoot = mid.GetRotation(stream);
            Quaternion solvedMidRotation = QuaternionExt.FromToRotation(
                rotatedTipPosition - solvedMidPosition,
                sample.Target - solvedMidPosition) * midAfterRoot;
            mid.SetRotation(stream, solvedMidRotation);

            Quaternion solvedTipRotation = Quaternion.Slerp(
                sourceTipRotation,
                targetRotation * targetRotationOffset,
                Mathf.Clamp01(targetRotationWeight.Get(stream)));

            // Solve one stable configuration first, then blend rotations. This is
            // deliberately different from the package TwoBoneIK implementation,
            // which lerps the target before a full solve and can change elbow branch
            // at small weights near extension.
            root.SetRotation(stream, EarthStableArmIkGeometry.BlendRotation(
                sourceRootRotation, solvedRootRotation, weight));
            mid.SetRotation(stream, EarthStableArmIkGeometry.BlendRotation(
                sourceMidRotation, solvedMidRotation, weight));
            tip.SetRotation(stream, EarthStableArmIkGeometry.BlendRotation(
                sourceTipRotation, solvedTipRotation, weight));
        }
    }

    public interface IEarthStableTwoBoneIkData
    {
        Transform Root { get; }
        Transform Mid { get; }
        Transform Tip { get; }
        Transform Target { get; }
        Transform Hint { get; }
        float MaximumReachFraction { get; }
        bool MaintainTargetRotationOffset { get; }
        string TargetRotationWeightProperty { get; }
    }

    [System.Serializable]
    public struct EarthStableTwoBoneIkData : IAnimationJobData, IEarthStableTwoBoneIkData
    {
        [SerializeField] private Transform root;
        [SerializeField] private Transform mid;
        [SerializeField] private Transform tip;
        [SyncSceneToStream, SerializeField] private Transform target;
        [SyncSceneToStream, SerializeField] private Transform hint;
        [SyncSceneToStream, SerializeField, Range(0f, 1f)] private float targetRotationWeight;
        [SerializeField, Range(.65f, .96f)] private float maximumReachFraction;
        [SerializeField] private bool maintainTargetRotationOffset;

        public Transform Root { get => root; set => root = value; }
        public Transform Mid { get => mid; set => mid = value; }
        public Transform Tip { get => tip; set => tip = value; }
        public Transform Target { get => target; set => target = value; }
        public Transform Hint { get => hint; set => hint = value; }
        public float TargetRotationWeight { get => targetRotationWeight; set => targetRotationWeight = Mathf.Clamp01(value); }
        public float MaximumReachFraction { get => maximumReachFraction; set => maximumReachFraction = Mathf.Clamp(value, .65f, .96f); }
        public bool MaintainTargetRotationOffset { get => maintainTargetRotationOffset; set => maintainTargetRotationOffset = value; }
        string IEarthStableTwoBoneIkData.TargetRotationWeightProperty =>
            ConstraintsUtils.ConstructConstraintDataPropertyName(nameof(targetRotationWeight));

        bool IAnimationJobData.IsValid() => root != null && mid != null && tip != null &&
                                            target != null && tip.IsChildOf(mid) && mid.IsChildOf(root);

        void IAnimationJobData.SetDefaultValues()
        {
            root = null;
            mid = null;
            tip = null;
            target = null;
            hint = null;
            targetRotationWeight = .45f;
            maximumReachFraction = .92f;
            maintainTargetRotationOffset = true;
        }
    }

    public sealed class EarthStableTwoBoneIkBinder<T> :
        AnimationJobBinder<EarthStableTwoBoneIkJob, T>
        where T : struct, IAnimationJobData, IEarthStableTwoBoneIkData
    {
        public override EarthStableTwoBoneIkJob Create(Animator animator, ref T data, Component component)
        {
            return new EarthStableTwoBoneIkJob
            {
                root = ReadWriteTransformHandle.Bind(animator, data.Root),
                mid = ReadWriteTransformHandle.Bind(animator, data.Mid),
                tip = ReadWriteTransformHandle.Bind(animator, data.Tip),
                target = ReadOnlyTransformHandle.Bind(animator, data.Target),
                hint = data.Hint != null ? ReadOnlyTransformHandle.Bind(animator, data.Hint) : default,
                targetRotationOffset = data.MaintainTargetRotationOffset
                    ? Quaternion.Inverse(data.Target.rotation) * data.Tip.rotation
                    : Quaternion.identity,
                targetRotationWeight = FloatProperty.Bind(
                    animator, component, data.TargetRotationWeightProperty),
                maximumReachFraction = data.MaximumReachFraction
            };
        }

        public override void Destroy(EarthStableTwoBoneIkJob job) { }
    }

    [DisallowMultipleComponent]
    public sealed class EarthStableTwoBoneIkConstraint : RigConstraint<
        EarthStableTwoBoneIkJob,
        EarthStableTwoBoneIkData,
        EarthStableTwoBoneIkBinder<EarthStableTwoBoneIkData>>
    {
    }
}
