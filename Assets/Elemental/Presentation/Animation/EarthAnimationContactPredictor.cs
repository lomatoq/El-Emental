using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Presentation.Animation
{
    /// <summary>
    /// Presentation-only landing forecast. It never writes to the motor, body,
    /// grounding state or collision response.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthAnimationContactPredictor : MonoBehaviour
    {
        private const int HitCapacity = 24;
        private static readonly ProfilerMarker PredictMarker =
            new ProfilerMarker("Elemental.Animation.ContactPredictor");

        private readonly RaycastHit[] _hits = new RaycastHit[HitCapacity];
        private PlanetMotor _motor;
        private EarthLandingCandidateSnapshot _lastValid;
        private float _missingSeconds;

        public EarthLandingCandidateSnapshot Latest { get; private set; }

        public void Configure(PlanetMotor configuredMotor)
        {
            _motor = configuredMotor;
            Latest = default;
            _lastValid = default;
            _missingSeconds = 0f;
        }

        public EarthLandingCandidateSnapshot Predict(
            float horizonSeconds,
            int stepCount,
            float graceSeconds,
            float deltaTime)
        {
            using (PredictMarker.Auto())
            {
                EarthLandingCandidateSnapshot current = PredictImmediate(horizonSeconds, stepCount);
                if (current.IsValid)
                {
                    _lastValid = current;
                    _missingSeconds = 0f;
                    Latest = current;
                    return current;
                }

                _missingSeconds += Mathf.Max(0f, deltaTime);
                if (_lastValid.IsValid && _missingSeconds <= Mathf.Clamp(graceSeconds, 0f, 0.25f))
                {
                    Latest = new EarthLandingCandidateSnapshot(
                        true,
                        Mathf.Max(0f, _lastValid.TimeToContact - _missingSeconds),
                        _lastValid.ImpactSpeed,
                        _lastValid.PlanarSpeed,
                        _lastValid.Point,
                        _lastValid.Normal,
                        _lastValid.SurfacePointVelocity,
                        _lastValid.SurfaceId,
                        _lastValid.Generation,
                        _lastValid.MovingSupport);
                    return Latest;
                }

                _lastValid = default;
                Latest = default;
                return default;
            }
        }

        private EarthLandingCandidateSnapshot PredictImmediate(float horizonSeconds, int stepCount)
        {
            if (_motor == null || _motor.Body == null || _motor.Capsule == null) return default;
            Rigidbody body = _motor.Body;
            CapsuleCollider capsule = _motor.Capsule;
            int steps = Mathf.Clamp(stepCount, 2, 8);
            float horizon = Mathf.Clamp(horizonSeconds, 0.15f, 1.2f);
            float stepSeconds = horizon / steps;
            ResolveWorldCapsule(capsule, out Vector3 basePointA, out Vector3 basePointB, out float radius);
            Vector3 motorUp = _motor.LocalUp.sqrMagnitude > 0.001f
                ? _motor.LocalUp.normalized
                : capsule.transform.up;
            // PlanetMotor declares stable support when its foot probe reaches the
            // walkable surface, before the physical capsule touches it. Forecast
            // that same semantic contact by extending only the lower endpoint;
            // the canonical collider and movement remain untouched.
            if (Vector3.Dot(basePointA - basePointB, motorUp) >= 0f)
                basePointB -= motorUp * _motor.GroundProbeDistance;
            else
                basePointA -= motorUp * _motor.GroundProbeDistance;
            Vector3 origin = capsule.transform.TransformPoint(capsule.center);
            Vector3 velocity = body.linearVelocity;
            Vector3 acceleration = _motor.GravityAcceleration;
            Vector3 previousCenter = origin;

            for (int step = 1; step <= steps; step++)
            {
                float time = step * stepSeconds;
                Vector3 nextCenter = origin + velocity * time + acceleration * (0.5f * time * time);
                Vector3 segment = nextCenter - previousCenter;
                float distance = segment.magnitude;
                if (distance > 0.0001f)
                {
                    Vector3 offset = previousCenter - origin;
                    int count = UnityEngine.Physics.CapsuleCastNonAlloc(
                        basePointA + offset,
                        basePointB + offset,
                        radius,
                        segment / distance,
                        _hits,
                        distance,
                        _motor.GroundMask,
                        QueryTriggerInteraction.Ignore);
                    RaycastHit selected = default;
                    float nearest = float.PositiveInfinity;
                    for (int index = 0; index < count; index++)
                    {
                        RaycastHit hit = _hits[index];
                        if (!IsAdmissible(hit, body, capsule)) continue;
                        if (hit.distance >= nearest) continue;
                        nearest = hit.distance;
                        selected = hit;
                    }

                    if (selected.collider != null)
                    {
                        float segment01 = Mathf.Clamp01(selected.distance / distance);
                        float contactTime = (step - 1 + segment01) * stepSeconds;
                        Vector3 contactVelocity = velocity + acceleration * contactTime;
                        ResolveSurface(selected, out Vector3 surfaceVelocity, out uint surfaceId,
                            out uint generation, out bool movingSupport);
                        Vector3 relative = contactVelocity - surfaceVelocity;
                        Vector3 currentRelative = velocity - surfaceVelocity;
                        float impactSpeed = Mathf.Max(
                            Mathf.Max(0f, -Vector3.Dot(relative, selected.normal)),
                            Mathf.Max(0f, -Vector3.Dot(relative, motorUp)));
                        impactSpeed = Mathf.Max(impactSpeed, Mathf.Max(
                            Mathf.Max(0f, -Vector3.Dot(currentRelative, selected.normal)),
                            Mathf.Max(0f, -Vector3.Dot(currentRelative, motorUp))));
                        float planarSpeed = Mathf.Max(
                            Vector3.ProjectOnPlane(relative, selected.normal).magnitude,
                            Vector3.ProjectOnPlane(relative, motorUp).magnitude);
                        planarSpeed = Mathf.Max(planarSpeed, Mathf.Max(
                            Vector3.ProjectOnPlane(currentRelative, selected.normal).magnitude,
                            Vector3.ProjectOnPlane(currentRelative, motorUp).magnitude));
                        return new EarthLandingCandidateSnapshot(
                            true,
                            contactTime,
                            impactSpeed,
                            planarSpeed,
                            ToFloat3(selected.point),
                            ToFloat3(selected.normal),
                            ToFloat3(surfaceVelocity),
                            surfaceId,
                            generation,
                            movingSupport);
                    }
                }
                previousCenter = nextCenter;
            }
            return default;
        }

        private bool IsAdmissible(RaycastHit hit, Rigidbody selfBody, CapsuleCollider selfCapsule)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider == selfCapsule || hit.rigidbody == selfBody) return false;
            if (hitCollider.transform.IsChildOf(selfBody.transform)) return false;
            float minimumSlopeDot = Mathf.Cos(_motor.MaximumSlopeAngle * Mathf.Deg2Rad);
            if (Vector3.Dot(hit.normal, _motor.LocalUp) < minimumSlopeDot) return false;
            Rigidbody hitBody = hit.rigidbody;
            if (hitBody == null || hitBody.isKinematic) return true;
            return hitCollider.GetComponentInParent(typeof(IMovingSurface)) is IMovingSurface;
        }

        private static void ResolveSurface(
            RaycastHit hit,
            out Vector3 pointVelocity,
            out uint surfaceId,
            out uint generation,
            out bool movingSupport)
        {
            IMovingSurface surface = hit.collider != null
                ? hit.collider.GetComponentInParent(typeof(IMovingSurface)) as IMovingSurface
                : null;
            if (surface != null && surface.SupportFrame.IsValid)
            {
                SupportFrameSnapshot frame = surface.SupportFrame;
                float3 value = frame.VelocityAt(ToFloat3(hit.point));
                pointVelocity = new Vector3(value.x, value.y, value.z);
                surfaceId = frame.SurfaceId;
                generation = frame.Generation;
                movingSupport = true;
                return;
            }

            Rigidbody body = hit.rigidbody;
            pointVelocity = body != null ? body.GetPointVelocity(hit.point) : Vector3.zero;
            int instanceId = hit.collider != null ? hit.collider.GetHashCode() : 0;
            surfaceId = unchecked((uint)(instanceId == int.MinValue ? int.MaxValue : Mathf.Abs(instanceId)));
            if (surfaceId == 0u) surfaceId = 1u;
            generation = 1u;
            movingSupport = body != null;
        }

        private static void ResolveWorldCapsule(
            CapsuleCollider capsule,
            out Vector3 pointA,
            out Vector3 pointB,
            out float radius)
        {
            Transform owner = capsule.transform;
            Vector3 scale = owner.lossyScale;
            Vector3 axis;
            float axisScale;
            float radialScale;
            switch (capsule.direction)
            {
                case 0:
                    axis = owner.right;
                    axisScale = Mathf.Abs(scale.x);
                    radialScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    break;
                case 2:
                    axis = owner.forward;
                    axisScale = Mathf.Abs(scale.z);
                    radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    break;
                default:
                    axis = owner.up;
                    axisScale = Mathf.Abs(scale.y);
                    radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    break;
            }
            radius = Mathf.Max(0.01f, capsule.radius * radialScale * 0.94f);
            float halfSegment = Mathf.Max(0f, capsule.height * axisScale * 0.5f - radius);
            Vector3 center = owner.TransformPoint(capsule.center);
            pointA = center + axis.normalized * halfSegment;
            pointB = center - axis.normalized * halfSegment;
        }

        private static float3 ToFloat3(Vector3 value) => new float3(value.x, value.y, value.z);
    }
}
