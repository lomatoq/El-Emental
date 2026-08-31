using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Post-processes controller output before Animation Rigging. It preserves the
    /// previously rendered local pose and velocities while the new controller state
    /// begins immediately, then decays the offsets with an exact critically-damped step.
    /// </summary>
    public struct EarthInertializationJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> BoneHandles;
        public NativeArray<EarthAnimationBoneOwnership> BoneOwnership;
        public NativeArray<byte> Initialized;
        public NativeArray<float3> PreviousTargetPositions;
        public NativeArray<quaternion> PreviousTargetRotations;
        public NativeArray<float3> PreviousOutputPositions;
        public NativeArray<quaternion> PreviousOutputRotations;
        public NativeArray<float3> OutputLinearVelocities;
        public NativeArray<float3> OutputAngularVelocities;
        public NativeArray<float3> PositionOffsets;
        public NativeArray<float3> PositionOffsetVelocities;
        public NativeArray<float3> RotationOffsets;
        public NativeArray<float3> RotationOffsetVelocities;
        public NativeArray<EarthAnimationGraphControl> Control;
        public NativeArray<EarthAnimationJobDiagnostics> Diagnostics;

        public void ProcessRootMotion(AnimationStream stream)
        {
            // PlanetMotor owns world/root translation. The job never writes root motion.
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!Control.IsCreated || !Diagnostics.IsCreated || Control.Length == 0 ||
                Diagnostics.Length == 0)
                return;

            EarthAnimationGraphControl control = Control[0];
            EarthAnimationJobDiagnostics diagnostics = Diagnostics[0];
            diagnostics.EvaluationCount = diagnostics.EvaluationCount == uint.MaxValue
                ? 1u
                : diagnostics.EvaluationCount + 1u;
            bool requested = control.RequestSequence != diagnostics.AppliedRequestSequence;
            bool useInertia = control.UsePoseInertialization != 0;
            bool wasActive = diagnostics.InertiaActive != 0;
            float deltaTime = math.clamp(
                math.isfinite(stream.deltaTime) ? stream.deltaTime : 0f,
                0f,
                EarthInertializationMath.MaximumStepSeconds);

            if (requested)
            {
                diagnostics.AppliedRequestSequence = control.RequestSequence;
                diagnostics.TransitionRequestCount++;
                if (wasActive) diagnostics.InterruptedTransitionCount++;
                diagnostics.InertiaActive = useInertia ? (byte)1 : (byte)0;
                diagnostics.ElapsedSeconds = 0f;
            }

            bool active = diagnostics.InertiaActive != 0 && useInertia;
            bool expired = active && diagnostics.ElapsedSeconds >= control.MaximumDurationSeconds;
            float maximumPositionOffset = 0f;
            float maximumRotationOffset = 0f;
            bool anyResidual = false;

            for (int index = 0; index < BoneHandles.Length; index++)
            {
                TransformStreamHandle handle = BoneHandles[index];
                if (!handle.IsValid(stream)) continue;

                float3 targetPosition = ToFloat3(handle.GetLocalPosition(stream));
                quaternion targetRotation = ToQuaternion(handle.GetLocalRotation(stream));
                targetPosition = EarthInertializationMath.Sanitize(targetPosition);
                targetRotation = EarthInertializationMath.Sanitize(targetRotation);

                if (Initialized[index] == 0)
                {
                    Initialized[index] = 1;
                    PreviousTargetPositions[index] = targetPosition;
                    PreviousTargetRotations[index] = targetRotation;
                    PreviousOutputPositions[index] = targetPosition;
                    PreviousOutputRotations[index] = targetRotation;
                    OutputLinearVelocities[index] = float3.zero;
                    OutputAngularVelocities[index] = float3.zero;
                }

                float3 targetLinearVelocity = EarthInertializationMath.LinearVelocity(
                    targetPosition,
                    PreviousTargetPositions[index],
                    deltaTime);
                float3 targetAngularVelocity = EarthInertializationMath.AngularVelocity(
                    targetRotation,
                    PreviousTargetRotations[index],
                    deltaTime);
                // The first destination sample has no prior destination sample.
                // Treat its target velocity as zero instead of differentiating
                // across two unrelated controller states; source output velocity
                // is still preserved in the composed offset velocity.
                if (requested)
                {
                    targetLinearVelocity = float3.zero;
                    targetAngularVelocity = float3.zero;
                }
                bool excluded = !EarthAnimationBoneMask.ShouldApplyInertialization(
                    BoneOwnership[index],
                    control.ActiveOwnership);

                EarthInertializationVectorState positionState = new EarthInertializationVectorState
                {
                    Offset = PositionOffsets[index],
                    Velocity = PositionOffsetVelocities[index]
                };
                EarthInertializationVectorState rotationState = new EarthInertializationVectorState
                {
                    Offset = RotationOffsets[index],
                    Velocity = RotationOffsetVelocities[index]
                };

                if (requested && useInertia && !excluded)
                {
                    positionState = EarthInertializationMath.ComposePosition(
                        PreviousOutputPositions[index],
                        OutputLinearVelocities[index],
                        targetPosition,
                        targetLinearVelocity,
                        control.MaximumPositionOffset,
                        control.MaximumLinearVelocity);
                    rotationState = EarthInertializationMath.ComposeRotation(
                        PreviousOutputRotations[index],
                        OutputAngularVelocities[index],
                        targetRotation,
                        targetAngularVelocity,
                        control.MaximumRotationOffsetRadians,
                        control.MaximumAngularVelocity);
                }
                else if (excluded || expired || !active)
                {
                    positionState = default;
                    rotationState = default;
                }
                else if (!requested)
                {
                    EarthInertializationMath.StepCriticallyDamped(
                        ref positionState,
                        control.PositionHalfLifeSeconds,
                        deltaTime);
                    EarthInertializationMath.StepCriticallyDamped(
                        ref rotationState,
                        control.RotationHalfLifeSeconds,
                        deltaTime);
                }

                float3 outputPosition = active && !excluded && !expired
                    ? EarthInertializationMath.ApplyPosition(targetPosition, in positionState)
                    : targetPosition;
                quaternion outputRotation = active && !excluded && !expired
                    ? EarthInertializationMath.ApplyRotation(targetRotation, in rotationState)
                    : targetRotation;

                OutputLinearVelocities[index] = EarthInertializationMath.LinearVelocity(
                    outputPosition,
                    PreviousOutputPositions[index],
                    deltaTime);
                OutputAngularVelocities[index] = EarthInertializationMath.AngularVelocity(
                    outputRotation,
                    PreviousOutputRotations[index],
                    deltaTime);
                PreviousTargetPositions[index] = targetPosition;
                PreviousTargetRotations[index] = targetRotation;
                PreviousOutputPositions[index] = outputPosition;
                PreviousOutputRotations[index] = outputRotation;
                PositionOffsets[index] = positionState.Offset;
                PositionOffsetVelocities[index] = positionState.Velocity;
                RotationOffsets[index] = rotationState.Offset;
                RotationOffsetVelocities[index] = rotationState.Velocity;

                float positionMagnitude = math.length(positionState.Offset);
                float rotationMagnitude = math.length(rotationState.Offset);
                maximumPositionOffset = math.max(maximumPositionOffset, positionMagnitude);
                maximumRotationOffset = math.max(maximumRotationOffset, rotationMagnitude);
                anyResidual |= positionMagnitude > 0.00005f || rotationMagnitude > 0.0005f ||
                               math.lengthsq(positionState.Velocity) > 0.000001f ||
                               math.lengthsq(rotationState.Velocity) > 0.00001f;

                handle.SetLocalPosition(stream, ToVector3(outputPosition));
                handle.SetLocalRotation(stream, ToQuaternion(outputRotation));
            }

            if (active && !requested) diagnostics.ElapsedSeconds += deltaTime;
            if (expired || (active && !anyResidual)) diagnostics.InertiaActive = 0;
            diagnostics.MaximumPositionOffset = maximumPositionOffset;
            diagnostics.MaximumRotationOffsetRadians = maximumRotationOffset;
            Diagnostics[0] = diagnostics;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);

        private static quaternion ToQuaternion(Quaternion value) =>
            new quaternion(value.x, value.y, value.z, value.w);

        private static Quaternion ToQuaternion(quaternion value) =>
            new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
    }
}
