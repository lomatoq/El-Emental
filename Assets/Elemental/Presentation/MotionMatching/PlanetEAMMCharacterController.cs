using Elemental.Runtime.Characters;
using Elemental.Simulation.Animation;
using Elemental.Simulation.Characters;
using MotionMatching;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Presentation.MotionMatching
{
    /// <summary>
    /// Read-only adapter from PlanetMotor to JLPM/EAMM. It predicts queries but
    /// never writes the gameplay transform, Rigidbody or visible rig.
    /// </summary>
    public sealed class PlanetEAMMCharacterController : MotionMatchingCharacterController,
        global::IObstacleAwareCharacterControler
    {
        public const string IdleQueryTag = "Idle";
        public const string ForwardQueryTag = "Forward";
        public const string BackwardQueryTag = "Backward";
        public const string LeftQueryTag = "Left";
        public const string RightQueryTag = "Right";
        public const string PivotQueryTag = "Pivot";
        public const string StartQueryTag = "Start";
        public const string StopQueryTag = "Stop";
        public const string UnsearchableQueryTag = "Unsearchable";

        private const int ObstacleCapacity = 24;
        private const int EnvironmentPredictionBuckets = 3;
        private const float QueryMoveDeadZone = 0.05f;
        private readonly Collider[] _overlaps = new Collider[ObstacleCapacity];
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private EAMMRuntimeProfile profile;
        [SerializeField] private SurfaceMotionResolver surfaceResolver;

        private NativeArray<(float2, float, float2)> _circles;
        private NativeArray<int> _circleCount;
        private NativeArray<(float2, float2, float2)> _ellipses;
        private NativeArray<int> _ellipseCount;
        private float2 _lastMove;
        private float2 _lastDirectionalMove;
        private bool _hasLocomotionQuery;
        private bool _locomotionQuery;
        private string _queryTag;

        public bool HasLocomotionQuery => _hasLocomotionQuery;
        public bool LocomotionQuery => _locomotionQuery;
        public string CurrentQueryTag => _queryTag;

        public void Configure(PlanetMotor configuredMotor, EAMMRuntimeProfile configuredProfile)
        {
            motor = configuredMotor;
            profile = configuredProfile;
        }

        private void Awake()
        {
            if (motor == null) motor = GetComponentInParent<PlanetMotor>();
            if (surfaceResolver == null) surfaceResolver = GetComponentInParent<SurfaceMotionResolver>();
            _circles = new NativeArray<(float2, float, float2)>(ObstacleCapacity, Allocator.Persistent);
            // UPC EAMM evaluates near/mid/far obstacle buckets and indexes
            // counts[0..2] unconditionally inside its Burst search job.
            _circleCount = new NativeArray<int>(EnvironmentPredictionBuckets, Allocator.Persistent);
            _ellipses = new NativeArray<(float2, float2, float2)>(1, Allocator.Persistent);
            _ellipseCount = new NativeArray<int>(EnvironmentPredictionBuckets, Allocator.Persistent);
        }

        private void OnDestroy()
        {
            if (_circles.IsCreated) _circles.Dispose();
            if (_circleCount.IsCreated) _circleCount.Dispose();
            if (_ellipses.IsCreated) _ellipses.Dispose();
            if (_ellipseCount.IsCreated) _ellipseCount.Dispose();
        }

        protected override void OnUpdate()
        {
            if (motor == null || MotionMatching == null) return;
            float2 move = motor.LastCommand.Move;
            if (math.lengthsq(move - _lastMove) > 0.55f * 0.55f)
                NotifyInputChangedQuickly();
            _lastMove = move;

            bool hasMoveIntent = math.lengthsq(move) > QueryMoveDeadZone * QueryMoveDeadZone;
            if (hasMoveIntent) _lastDirectionalMove = move;
            bool locomoting = hasMoveIntent || GetTargetSpeed() > 0.22f;
            float2 queryMove = hasMoveIntent ? move : _lastDirectionalMove;
            string queryTag = ResolveQueryTag(queryMove, motor.UsesTankSteering, locomoting);
            if (!_hasLocomotionQuery || !string.Equals(queryTag, _queryTag,
                    System.StringComparison.Ordinal))
            {
                MotionMatching.SetQueryTag(queryTag);
                _queryTag = queryTag;
            }
            _locomotionQuery = locomoting;
            _hasLocomotionQuery = true;

            // The hidden simulation bone follows the authoritative root solely as
            // a query origin. Its resulting motion is never copied back.
            transform.SetPositionAndRotation(motor.transform.position, motor.transform.rotation);
        }

        public static string ResolveQueryTag(
            float2 move,
            bool usesTankSteering,
            bool locomoting)
        {
            if (!locomoting) return IdleQueryTag;

            if (usesTankSteering)
                return move.y < -QueryMoveDeadZone
                    ? BackwardQueryTag
                    : ForwardQueryTag;

            float horizontal = math.abs(move.x);
            float vertical = math.abs(move.y);
            if (horizontal > vertical)
                return move.x < 0f ? LeftQueryTag : RightQueryTag;
            return move.y < 0f ? BackwardQueryTag : ForwardQueryTag;
        }

        public override float3 GetWorldInitPosition() => GetPosition();
        public override float3 GetWorldInitDirection() => motor != null ? (float3)motor.FacingForward : math.forward();
        public override float3 GetPosition() => motor != null ? (float3)motor.transform.position : float3.zero;

        public override float GetTargetSpeed()
        {
            if (motor == null || motor.Body == null) return 0f;
            float3 up = motor.LocalUp;
            float3 velocity = motor.Body.linearVelocity;
            float stride = surfaceResolver != null && surfaceResolver.Current != null
                ? surfaceResolver.Current.StrideScale
                : 1f;
            return math.length(velocity - up * math.dot(velocity, up)) * stride;
        }

        public override void GetTrajectoryFeature(
            MotionMatchingData.TrajectoryFeature feature,
            int index,
            Transform character,
            NativeArray<float> output)
        {
            if (motor == null) return;
            GravityMotionFrame frame = GravityMotionFrame.Create(
                motor.transform.position,
                motor.LocalUp,
                motor.FacingForward);
            float seconds = PredictionSeconds(feature, index);
            float3 velocity = motor.Body != null ? (float3)motor.Body.linearVelocity : float3.zero;
            float3 tangentVelocity = velocity - frame.Up * math.dot(velocity, frame.Up);
            float2 intent = motor.LastCommand.Move;
            ResolveTrajectoryIntent(
                frame,
                intent,
                seconds,
                out float3 intendedTravelDirection,
                out float3 predictedFacing,
                out float intentMagnitude);
            float stride = surfaceResolver != null && surfaceResolver.Current != null
                ? surfaceResolver.Current.StrideScale
                : 1f;
            float caution = surfaceResolver != null && surfaceResolver.Current != null
                ? surfaceResolver.Current.Caution
                : 0f;
            float targetSpeed = math.max(math.length(tangentVelocity), intentMagnitude * 6.5f) *
                                stride * math.lerp(1f, 0.72f, caution);
            float3 predictedVelocity = math.lerp(
                tangentVelocity,
                intendedTravelDirection * targetSpeed,
                math.saturate(seconds * 5f));

            float3 localValue;
            if (feature.FeatureType == MotionMatchingData.TrajectoryFeature.Type.Direction)
                // JLPM's direction feature describes where the character faces,
                // not where its root translates. That distinction is essential
                // for in-place pivots and backward tank locomotion: both may have
                // zero/reversed travel while the body still turns forward.
                localValue = frame.WorldDirectionToLocal(predictedFacing);
            else
                localValue = frame.WorldDirectionToLocal(predictedVelocity * seconds);
            WriteFeature(feature, localValue, output);
        }

        private void ResolveTrajectoryIntent(
            in GravityMotionFrame frame,
            float2 intent,
            float predictionSeconds,
            out float3 intendedTravelDirection,
            out float3 predictedFacing,
            out float intentMagnitude)
        {
            if (motor != null && motor.UsesTankSteering)
            {
                predictedFacing = PlanetTankSteeringSolver.Turn(
                    frame.Up,
                    frame.Forward,
                    intent.x,
                    motor.TankTurnRateDegrees,
                    predictionSeconds);
                float signedForward = math.clamp(intent.y, -1f, 1f);
                intendedTravelDirection = signedForward < 0f ? -predictedFacing : predictedFacing;
                intentMagnitude = math.abs(signedForward);
                return;
            }

            intendedTravelDirection = math.normalizesafe(
                frame.Right * intent.x + frame.Forward * intent.y,
                frame.Forward);
            intentMagnitude = math.saturate(math.length(intent));
            predictedFacing = intentMagnitude > 0.001f
                ? intendedTravelDirection
                : frame.Forward;
        }

        public override void GetEnvironmentFeature(
            MotionMatchingData.TrajectoryFeature feature,
            int index,
            Transform character,
            NativeArray<float> output)
        {
            if (motor == null) return;
            if (feature.Name == "FutureEllipse")
            {
                for (int i = 0; i < output.Length; i++) output[i] = 0f;
                return;
            }
            float seconds = PredictionSeconds(feature, index);
            GravityMotionFrame frame = GravityMotionFrame.Create(
                motor.transform.position,
                motor.LocalUp,
                motor.FacingForward);
            float3 velocity = motor.Body != null ? (float3)motor.Body.linearVelocity : float3.zero;
            float3 predicted = frame.Origin + velocity * seconds;
            float minimumHeight = -1.1f;
            float maximumHeight = 2.2f;
            if (Physics.Raycast(
                    (Vector3)(predicted + frame.Up * 1.25f),
                    (Vector3)(-frame.Up),
                    out RaycastHit hit,
                    3.5f,
                    motor.GroundMask,
                    QueryTriggerInteraction.Ignore))
            {
                minimumHeight = math.dot((float3)hit.point - predicted, frame.Up);
            }
            if (Physics.Raycast(
                    (Vector3)(predicted + frame.Up * 0.1f),
                    (Vector3)frame.Up,
                    out RaycastHit ceiling,
                    3.5f,
                    profile != null ? profile.ObstacleMask : ~0,
                    QueryTriggerInteraction.Ignore))
                maximumHeight = math.dot((float3)ceiling.point - predicted, frame.Up);
            if (output.Length > 0) output[0] = minimumHeight;
            if (output.Length > 1) output[1] = maximumHeight;
        }

        public (
            NativeArray<(float2, float, float2)>,
            NativeArray<int>,
            NativeArray<(float2, float2, float2)>,
            NativeArray<int>) GetNearbyObstacles(Transform character, float obstacleDistanceThreshold)
        {
            for (int bucket = 0; bucket < EnvironmentPredictionBuckets; bucket++)
            {
                _circleCount[bucket] = 0;
                _ellipseCount[bucket] = 0;
            }
            if (motor == null) return (_circles, _circleCount, _ellipses, _ellipseCount);

            GravityMotionFrame frame = GravityMotionFrame.Create(
                motor.transform.position,
                motor.LocalUp,
                motor.FacingForward);
            float radius = math.max(obstacleDistanceThreshold, profile != null ? profile.ObstacleRadius : 1.35f);
            int hits = Physics.OverlapSphereNonAlloc(
                motor.transform.position,
                radius,
                _overlaps,
                profile != null ? profile.ObstacleMask : ~0,
                QueryTriggerInteraction.Ignore);
            int count = 0;
            for (int i = 0; i < hits && count < _circles.Length; i++)
            {
                Collider obstacle = _overlaps[i];
                if (obstacle == null || obstacle == motor.Capsule || obstacle.transform.IsChildOf(motor.transform)) continue;
                Vector3 closest = obstacle is MeshCollider meshCollider && !meshCollider.convex
                    ? obstacle.bounds.ClosestPoint(motor.transform.position)
                    : obstacle.ClosestPoint(motor.transform.position);
                float3 local = frame.WorldPointToLocal(closest);
                Bounds bounds = obstacle.bounds;
                float obstacleRadius = math.max(0.08f, math.min(bounds.extents.x, bounds.extents.z));
                _circles[count++] = (new float2(local.x, local.z), obstacleRadius, float2.zero);
            }
            _circleCount[0] = count;
            return (_circles, _circleCount, _ellipses, _ellipseCount);
        }

        private float PredictionSeconds(MotionMatchingData.TrajectoryFeature feature, int index)
        {
            int frame = feature.FramesPrediction != null && index >= 0 && index < feature.FramesPrediction.Length
                ? feature.FramesPrediction[index]
                : index + 1;
            float rate = profile != null ? profile.DatabaseRate : 30f;
            return math.min(frame / rate, profile != null ? profile.PredictionSeconds : 0.85f);
        }

        private static void WriteFeature(
            MotionMatchingData.TrajectoryFeature feature,
            float3 value,
            NativeArray<float> output)
        {
            int write = 0;
            if (!feature.ZeroX && write < output.Length) output[write++] = value.x;
            if (!feature.ZeroY && write < output.Length) output[write++] = value.y;
            if (!feature.ZeroZ && write < output.Length) output[write] = value.z;
        }
    }
}
