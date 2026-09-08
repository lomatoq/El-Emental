using System.Collections.Generic;
using Elemental.Presentation.MotionMatching;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Characters;
using MotionMatching;
using UnityEngine;
using Unity.Profiling;

namespace Elemental.Presentation.Animation
{
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class LocomotionRhythmController : MonoBehaviour
    {
        [SerializeField] private LocomotionClipCatalog catalog;
        private PlanetMotor _motor;
        private HumanoidCharacterPresentation _presentation;
        private EarthAnimationDriver _driver;
        private EAMMBasePoseBridge _bridge;
        private PoseSet _poseSet;
        private float[] _sourceSpeeds, _leftPhases, _rightPhases, _cycleDistances;
        private int _lastSourceClip = -1;
        private float _lastSourceFrame;
        private readonly List<AnimatorClipInfo> _clips = new List<AnimatorClipInfo>(32);
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Elemental.Locomotion.Rhythm");
        public float PlaybackRate { get; private set; } = 1f;
        public float StrideScale { get; private set; } = 1f;
        public float AuthoredSpeed { get; private set; }
        public float MeasuredCycleDistance { get; private set; }
        public float VisualStrideScale { get; private set; } = 1f;
        public bool HasSourcePhases => _poseSet != null && _bridge != null &&
            _bridge.SourceController != null && _bridge.SourceController.PoseSet == _poseSet;
        public float LeftPhase => SourcePhase(_leftPhases);
        public float RightPhase => SourcePhase(_rightPhases);
        public LocomotionMotionSample Motion => _motor != null ? _motor.LocomotionMotion : default;
        public void Configure(LocomotionClipCatalog value) => catalog = value;
        public bool HasCompatibleSourceTags(PoseSet poses)
        {
            if (poses == null || catalog == null) return false;
            foreach (var entry in catalog.Entries)
                if (string.IsNullOrEmpty(entry.SourceQueryTag) || !poses.TryGetTag(entry.SourceQueryTag, out _)) return false;
            return catalog.Entries.Length > 0;
        }

        public string ResolveSpeedQuery(string directionTag, float actualSpeed, string previousTag)
        {
            if (catalog == null || directionTag == PlanetEAMMCharacterController.IdleQueryTag) return directionTag;
            LocomotionClipCatalog.Entry best = null, previous = null;
            float error = float.PositiveInfinity;
            foreach (var entry in catalog.Entries)
            {
                if (string.IsNullOrEmpty(entry.SourceQueryTag) || entry.NominalSpeed < .12f) continue;
                Vector2 position = entry.BlendPosition;
                string direction = Mathf.Abs(position.x) > Mathf.Abs(position.y)
                    ? (position.x < 0f ? PlanetEAMMCharacterController.LeftQueryTag : PlanetEAMMCharacterController.RightQueryTag)
                    : position.y < 0f ? PlanetEAMMCharacterController.BackwardQueryTag : PlanetEAMMCharacterController.ForwardQueryTag;
                if (direction != directionTag) continue;
                if (entry.SourceQueryTag == previousTag) previous = entry;
                float candidateError = Mathf.Abs(entry.NominalSpeed * VisualStrideScale - actualSpeed);
                if (candidateError < error) { error = candidateError; best = entry; }
            }
            // Avoid switching walk/run on tiny speed fluctuations near the boundary.
            if (previous != null && Mathf.Abs(previous.NominalSpeed * VisualStrideScale - actualSpeed) <= error + .15f) return previous.SourceQueryTag;
            return best != null ? best.SourceQueryTag : directionTag;
        }

        private void OnDisable()
        {
            if (_bridge == null || _bridge.SourceController == null) return;
            _bridge.SourceController.LocomotionPlaybackRate = 1f;
            _bridge.SourceController.PresentationClockMultiplier = 1f;
        }

        private void Awake()
        {
            _motor = GetComponentInParent<PlanetMotor>();
            _presentation = GetComponent<HumanoidCharacterPresentation>();
            _driver = GetComponent<EarthAnimationDriver>();
            _bridge = GetComponent<EAMMBasePoseBridge>();
        }

        private void Update()
        {
            using var scope = Marker.Auto();
            if (_motor == null || _presentation == null) return;
            // Metadata is measured on a canonical human in metres. The visible
            // avatar's import scale is already in these bone positions: applying
            // lossyScale (e.g. 2.42) again would double-scale the gait.
            if (catalog != null && catalog.ReferenceLegLengthMeters > .01f)
            {
                float leg = LocomotionClipCatalog.MeasureLegLength(_presentation.Animator);
                if (leg > .01f) VisualStrideScale = leg / catalog.ReferenceLegLengthMeters;
            }
            if (_driver == null) _driver = GetComponent<EarthAnimationDriver>();
            if (_bridge == null) _bridge = GetComponent<EAMMBasePoseBridge>();
            MotionMatchingController source = _bridge != null ? _bridge.SourceController : null;
            if (source != null && source.PoseSet != null && source.PoseSet != _poseSet) BakeSource(source.PoseSet);
            bool allowed = _motor.HasStableSupport && !_motor.HasDirectedExternalMotion && !_motor.IsImpactStunned &&
                _presentation.CurrentAuthoredAction is EarthAuthoredActionId.None or EarthAuthoredActionId.Locomotion;
            float nominal = 0f;
            bool sourceGait = false;
            if (source != null && _poseSet != null && _bridge.AppliedEammMasterWeight > .5f)
            {
                for (int i = 0; i < _poseSet.NumberClips; i++)
                {
                    PoseSet.AnimationClip clip = _poseSet.GetAnimationClip(i);
                    if (source.CurrentFrame >= clip.Start && source.CurrentFrame < clip.End)
                    {
                        nominal = _sourceSpeeds[i] * VisualStrideScale;
                        sourceGait = allowed && nominal > .12f;
                        if (sourceGait)
                        {
                            bool discontinuity = i != _lastSourceClip || Mathf.Abs(source.ContinuousFrame - _lastSourceFrame) > 5f;
                            MeasuredCycleDistance = _cycleDistances[source.CurrentFrame] * VisualStrideScale;
                            _motor.SetLocomotionGaitReference(MeasuredCycleDistance * StrideScale, LeftPhase, discontinuity);
                        }
                        _lastSourceClip = i; _lastSourceFrame = source.ContinuousFrame;
                        break;
                    }
                }
            }
            else if (_driver != null && catalog != null)
            {
                _driver.GetCurrentAnimatorClipInfo(0, _clips);
                float weight = 0f;
                for (int i = 0; i < _clips.Count; i++)
                {
                    var entry = catalog.Find(_clips[i].clip);
                    if (entry == null || entry.NominalSpeed < .12f) continue;
                    nominal += entry.NominalSpeed * VisualStrideScale * _clips[i].weight; weight += _clips[i].weight;
                }
                if (weight > .001f) nominal /= weight;
            }
            AuthoredSpeed = nominal;
            var sample = LocomotionRhythmSolver.Resolve(Motion.Speed, nominal, allowed);
            float targetRate = sample.PlaybackRate;
            if (sourceGait)
            {
                // Small phase correction keeps the visible contact clock aligned to
                // the distance clock without moving the capsule or jumping the pose.
                float phaseError = Mathf.DeltaAngle(LeftPhase * 360f, Motion.Phase01 * 360f) / 360f;
                targetRate = Mathf.Clamp(targetRate + Mathf.Clamp(phaseError * .6f, -.08f, .08f), .8f, 1.25f);
            }
            float blend = 1f - Mathf.Exp(-Time.deltaTime / .12f);
            PlaybackRate = Mathf.Lerp(PlaybackRate, targetRate, blend);
            float targetStride = allowed && nominal >= .12f && Motion.Speed >= .12f
                ? Mathf.Clamp(Motion.Speed / (nominal * PlaybackRate), .9f, 1.1f) : 1f;
            StrideScale = Mathf.Lerp(StrideScale, targetStride, blend);
            if (source != null)
            {
                source.LocomotionPlaybackRate = allowed ? PlaybackRate : 1f;
                source.PresentationClockMultiplier = _driver != null ? _driver.PresentationClockMultiplier : 1f;
            }
        }

        public Vector3 WarpFoot(Vector3 animated, Vector3 hipOrigin)
        {
            if (_motor == null || !_motor.HasStableSupport || _motor.HasDirectedExternalMotion ||
                Motion.Speed < .12f || _presentation.CurrentAuthoredAction is not (EarthAuthoredActionId.None or EarthAuthoredActionId.Locomotion)) return animated;
            Vector3 velocity = Motion.RelativeVelocity;
            Vector3 direction = velocity.normalized;
            return animated + direction * (Vector3.Dot(animated - hipOrigin, direction) * (StrideScale - 1f));
        }

        private float SourcePhase(float[] phases)
        {
            if (!HasSourcePhases || phases == null) return 0f;
            float frame = _bridge.SourceController.ContinuousFrame;
            int a = Mathf.Clamp(Mathf.FloorToInt(frame), 0, phases.Length - 1);
            int b = Mathf.Min(a + 1, phases.Length - 1);
            for (int i = 0; i < _poseSet.NumberClips; i++)
            {
                var clip = _poseSet.GetAnimationClip(i);
                if (a >= clip.Start && a < clip.End) { if (b >= clip.End) b = clip.Start; break; }
            }
            float delta = phases[b] - phases[a];
            if (delta < -.5f) delta += 1f;
            return Mathf.Repeat(phases[a] + delta * (frame - Mathf.Floor(frame)), 1f);
        }

        private void BakeSource(PoseSet poses)
        {
            _poseSet = poses;
            _sourceSpeeds = new float[poses.NumberClips];
            _leftPhases = new float[poses.NumberPoses]; _rightPhases = new float[poses.NumberPoses];
            _cycleDistances = new float[poses.NumberPoses];
            for (int i = 0; i < poses.NumberClips; i++)
            {
                PoseSet.AnimationClip clip = poses.GetAnimationClip(i);
                int count = clip.End - clip.Start;
                var left = new bool[count]; var right = new bool[count]; var phases = new float[count];
                float speed = 0f;
                for (int j = 0; j < count; j++)
                {
                    poses.GetPose(clip.Start + j, out PoseVector pose);
                    left[j] = pose.LeftFootContact; right[j] = pose.RightFootContact;
                    var velocity = pose.JointLocalVelocities[0];
                    speed += Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
                }
                _sourceSpeeds[i] = count > 0 ? speed / count : 0f;
                LocomotionRhythmSolver.FillContactPhases(left, phases);
                System.Array.Copy(phases, 0, _leftPhases, clip.Start, count);
                for (int j = 0; j < count; j++)
                {
                    float phaseStep = phases[(j + 1) % count] - phases[j];
                    if (phaseStep < 0f) phaseStep += 1f;
                    float cycleFrames = phaseStep > .00001f ? 1f / phaseStep : count;
                    _cycleDistances[clip.Start + j] = Mathf.Max(.2f, _sourceSpeeds[i] * poses.FrameTime * cycleFrames);
                }
                LocomotionRhythmSolver.FillContactPhases(right, phases);
                System.Array.Copy(phases, 0, _rightPhases, clip.Start, count);
            }
        }
    }
}
