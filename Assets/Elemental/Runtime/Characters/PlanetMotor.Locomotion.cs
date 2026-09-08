using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class PlanetMotor
    {
        [SerializeField, Min(.2f)] private float locomotionCycleDistance = 2f;
        private float _distancePhase, _locomotionDistance;
        public LocomotionMotionSample LocomotionMotion { get; private set; }

        // Presentation supplies measured gait metadata, never displacement or force.
        // The authoritative motor advances phase by distance between source transitions.
        public void SetLocomotionGaitReference(float cycleDistance, float sourcePhase, bool resynchronize)
        {
            if (float.IsFinite(cycleDistance) && cycleDistance >= .2f)
                locomotionCycleDistance = Mathf.Clamp(cycleDistance, .2f, 12f);
            if (resynchronize && float.IsFinite(sourcePhase)) _distancePhase = Mathf.Repeat(sourcePhase, 1f);
        }

        private float PublishLocomotion(Vector3 relativeVelocity, Vector3 desiredVelocity, bool pulseAllowed)
        {
            float distance = HasStableSupport ? relativeVelocity.magnitude * Time.fixedDeltaTime : 0f;
            _locomotionDistance += distance;
            _distancePhase = LocomotionRhythmSolver.AdvancePhase(_distancePhase, distance, locomotionCycleDistance);
            float pulse = LocomotionRhythmSolver.Pulse(_distancePhase,
                pulseAllowed && relativeVelocity.magnitude > .2f && desiredVelocity.magnitude > .2f);
            LocomotionMotion = new LocomotionMotionSample(_tick, MovingSurfaceId, relativeVelocity,
                desiredVelocity, _localUp, FacingForward, _distancePhase, _locomotionDistance, pulse, HasStableSupport);
            return pulse;
        }

        private void ResetLocomotionMotion()
        {
            _distancePhase = _locomotionDistance = 0f;
            LocomotionMotion = default;
        }
    }
}
