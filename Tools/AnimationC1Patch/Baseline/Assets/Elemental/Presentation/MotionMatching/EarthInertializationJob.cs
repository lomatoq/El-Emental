using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

        [NativeDisableParallelForRestriction] public NativeArray<Quaternion> PreviousOutput;
        [NativeDisableParallelForRestriction] public NativeArray<Quaternion> RotationOffsets;
        [NativeDisableParallelForRestriction] public NativeArray<byte> Initialized;
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
            float remaining = Mathf.Pow(0.5f, Mathf.Max(0f, stream.deltaTime) / halfLife);

            for (int i = 0; i < Handles.Length; i++)
            {
                TransformStreamHandle handle = Handles[i];
                if (!handle.IsValid(stream)) continue;

                Quaternion animatorLocal = handle.GetLocalRotation(stream);
                float eammWeight = master * Mathf.Clamp01(EammBoneWeights[i]);
                Quaternion targetLocal = eammWeight > 0f
                    ? Quaternion.Slerp(animatorLocal, EammLocalRotations[i], eammWeight)
                    : animatorLocal;
                if (Initialized[i] == 0)
                {
                    Initialized[i] = 1;
                    PreviousOutput[i] = targetLocal;
                    RotationOffsets[i] = Quaternion.identity;
                }

                if (captureTransition)
                    RotationOffsets[i] = Quaternion.Inverse(targetLocal) * PreviousOutput[i];

                byte contactGroup = ContactGroups[i];
                bool planted = contactGroup == 1 && FootContacts[0] > 0.5f ||
                               contactGroup == 2 && FootContacts[1] > 0.5f;
                Quaternion offset = planted
                    ? Quaternion.identity
                    : Quaternion.Slerp(Quaternion.identity, RotationOffsets[i], remaining);
                RotationOffsets[i] = offset;
                Quaternion output = targetLocal * offset;
                handle.SetLocalRotation(stream, output);
                PreviousOutput[i] = output;
            }

            if (captureTransition && AppliedTransitionSerial.Length > 0)
                AppliedTransitionSerial[0] = requestedSerial;

        }
    }
}
