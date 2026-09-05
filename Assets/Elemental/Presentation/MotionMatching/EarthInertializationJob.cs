using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;

namespace Elemental.Presentation.MotionMatching
{
    /// <summary>
    /// Final rotation-continuity pass for both Animator and EAMM poses. It keeps
    /// cached per-bone output/offset state and exponentially decays the offset
    /// after an explicit semantic transition. Gameplay root translation is not
    /// represented here; planted feet are removed from the generic decay.
    /// </summary>
    public struct EarthInertializationJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> Handles;
        [ReadOnly] public NativeArray<Quaternion> EammLocalRotations;
        [ReadOnly] public NativeArray<float> EammBoneWeights;
        [ReadOnly] public NativeArray<float> EammMasterWeight;
        [ReadOnly] public NativeArray<byte> ContactGroups;
        [ReadOnly] public NativeArray<float> FootContacts;
        [ReadOnly] public NativeArray<int> TransitionSerial;
        [ReadOnly] public NativeArray<float> HalfLifeSeconds;
        [ReadOnly] public NativeArray<float> InertializationEnabled;

        [NativeDisableParallelForRestriction]
        public NativeArray<EarthRotationInertializationState> RotationStates;
        [NativeDisableParallelForRestriction] public NativeArray<int> AppliedTransitionSerial;

        public void ProcessRootMotion(AnimationStream stream)
        {
            // PlanetMotor is the only gameplay-root writer.
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            float master = EammMasterWeight.Length > 0 ? Mathf.Clamp01(EammMasterWeight[0]) : 0f;
            int requestedSerial = TransitionSerial.Length > 0 ? TransitionSerial[0] : 0;
            int appliedSerial = AppliedTransitionSerial.Length > 0 ? AppliedTransitionSerial[0] : 0;
            bool captureTransition = requestedSerial != appliedSerial;
            float halfLife = HalfLifeSeconds.Length > 0
                ? Mathf.Max(0.01f, HalfLifeSeconds[0])
                : 0.08f;
            bool genericInertializationEnabled =
                InertializationEnabled.Length == 0 || InertializationEnabled[0] > 0.5f;
            for (int i = 0; i < Handles.Length; i++)
            {
                TransformStreamHandle handle = Handles[i];
                if (!handle.IsValid(stream)) continue;

                Quaternion animatorLocal = handle.GetLocalRotation(stream);
                float eammWeight = master * Mathf.Clamp01(EammBoneWeights[i]);
                Quaternion targetLocal = eammWeight > 0f
                    ? Quaternion.Slerp(animatorLocal, EammLocalRotations[i], eammWeight)
                    : animatorLocal;
                byte contactGroup = ContactGroups[i];
                bool planted = contactGroup == 1 && FootContacts[0] > 0.5f ||
                               contactGroup == 2 && FootContacts[1] > 0.5f;
                EarthRotationInertializationState state = RotationStates[i];
                quaternion output = EarthRotationInertialization.Step(
                    ref state,
                    new quaternion(targetLocal.x, targetLocal.y, targetLocal.z, targetLocal.w),
                    halfLife,
                    stream.deltaTime,
                    captureTransition,
                    planted || !genericInertializationEnabled,
                    out _);
                RotationStates[i] = state;
                handle.SetLocalRotation(stream, new Quaternion(
                    output.value.x,
                    output.value.y,
                    output.value.z,
                    output.value.w));
            }

            if (captureTransition && AppliedTransitionSerial.Length > 0)
                AppliedTransitionSerial[0] = requestedSerial;

        }
    }
}
