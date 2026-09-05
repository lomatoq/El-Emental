using System;
using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    [Serializable]
    public struct EarthMagicClipTiming
    {
        public float AcquireEnd, RootEnd, LoadEnd, Contact, Sustain, RecoverEnd;
        public float AcquireSeconds, RootSeconds, LoadSeconds, StrikeSeconds, SustainSeconds, RecoverSeconds;

        public static EarthMagicClipTiming Default => new EarthMagicClipTiming
        {
            AcquireEnd = 0.10f, RootEnd = 0.22f, LoadEnd = 0.38f,
            Contact = 0.52f, Sustain = 0.68f, RecoverEnd = 0.98f,
            AcquireSeconds = 0.10f, RootSeconds = 0.12f, LoadSeconds = 0.16f,
            StrikeSeconds = 0.10f, SustainSeconds = 0.18f, RecoverSeconds = 0.22f
        };

        public bool IsValid => AcquireEnd >= 0f && AcquireEnd <= RootEnd && RootEnd <= LoadEnd &&
            LoadEnd <= Contact && Contact <= Sustain && Sustain <= RecoverEnd && RecoverEnd <= 1f &&
            AcquireSeconds > 0f && RootSeconds > 0f && LoadSeconds > 0f && StrikeSeconds > 0f &&
            SustainSeconds > 0f && RecoverSeconds > 0f;

        public float End(EarthCastPhase phase) => phase switch
        {
            EarthCastPhase.Acquire => AcquireEnd, EarthCastPhase.Root => RootEnd,
            EarthCastPhase.Load => LoadEnd, EarthCastPhase.Strike => Contact,
            EarthCastPhase.Sustain => Sustain, _ => RecoverEnd
        };
        public float Seconds(EarthCastPhase phase) => phase switch
        {
            EarthCastPhase.Acquire => AcquireSeconds, EarthCastPhase.Root => RootSeconds,
            EarthCastPhase.Load => LoadSeconds, EarthCastPhase.Strike => StrikeSeconds,
            EarthCastPhase.Sustain => SustainSeconds, _ => RecoverSeconds
        };

        public float Start(EarthCastPhase phase) => phase switch
        {
            EarthCastPhase.Acquire => 0f,
            EarthCastPhase.Root => AcquireEnd,
            EarthCastPhase.Load => RootEnd,
            EarthCastPhase.Strike => LoadEnd,
            EarthCastPhase.Sustain => Contact,
            EarthCastPhase.Recover => Sustain,
            _ => 0f
        };
    }

    /// <summary>Continuous visual clip clock; never controls the gameplay event tick.</summary>
    public struct EarthMagicClipClock
    {
        public const float MaximumNormalizedSpeedPerSecond = 2f;
        public const float RaiseWallMaximumNormalizedSpeedPerSecond = 0.27f;
        public const float RaisePlatformMaximumNormalizedSpeedPerSecond = 0.60f;
        public const float PullStoneMaximumNormalizedSpeedPerSecond = 0.65f;
        public const float HeavyThrowMaximumNormalizedSpeedPerSecond = 0.62f;
        public const float VectorPushMaximumNormalizedSpeedPerSecond = 0.85f;
        public const float GravityRepairMaximumNormalizedSpeedPerSecond = 0.65f;
        public const float WaveResonanceMaximumNormalizedSpeedPerSecond = 0.24f;
        public const float PillarMaximumNormalizedSpeedPerSecond = 1.70f;
        public const float ArmorAssembleMaximumNormalizedSpeedPerSecond = 0.65f;
        public const float ArmorBarrageMaximumNormalizedSpeedPerSecond = 0.72f;
        public const float QuickPunchMaximumNormalizedSpeedPerSecond = 1.5f;
        private int _slot;
        private uint _sequence;
        private EarthCastPhase _phase;
        private float _phaseStart, _elapsed;
        private bool _active;
        public float NormalizedTime { get; private set; }

        public float Step(int slot, uint sequence, EarthCastPhase phase, bool active,
            in EarthMagicClipTiming timing, float deltaTime,
            bool startAtContact = false)
        {
            if (active && (!_active || slot != _slot || sequence != _sequence))
            {
                _slot = slot; _sequence = sequence; _phase = EarthCastPhase.Idle;
                _phaseStart = _elapsed = 0f;
                // Each accepted sequence owns the inactive A/B Animator buffer.
                // Its time can restart at frame zero while the outgoing state's
                // independent parameters remain frozen through the crossfade.
                NormalizedTime = startAtContact ? timing.Contact : 0f;
            }
            _active = active;
            EarthCastPhase requested = active ? phase : EarthCastPhase.Recover;
            if (requested == EarthCastPhase.Idle) requested = EarthCastPhase.Recover;
            if (requested != _phase)
            {
                // Fixed phases can advance between rendered frames. Preserve the
                // last sampled pose and move continuously toward the new marker;
                // snapping to timing.Start skips a large section of long source
                // clips in one render and visibly folds the arm chain.
                _phaseStart = NormalizedTime; _elapsed = 0f; _phase = requested;
            }
            float boundedDelta = math.clamp(math.isfinite(deltaTime) ? deltaTime : 0f, 0f, 0.1f);
            _elapsed += boundedDelta;
            float target = math.max(_phaseStart, timing.End(requested));
            float t = math.saturate(_elapsed / math.max(0.01f, timing.Seconds(requested)));
            // Ease the velocity at marker boundaries, not the event itself.
            float eased = t * t * (3f - 2f * t);
            float next = math.lerp(_phaseStart, target, eased);
            float maximumSpeed = MaximumSpeedForSlot(slot);
            NormalizedTime = math.min(
                next,
                NormalizedTime + maximumSpeed * boundedDelta);
            return NormalizedTime;
        }

        public static float MaximumSpeedForSlot(int slot) => slot switch
        {
            // These rates are normalized clip units per real second. They are
            // curated from the shipped clip lengths and the native 30/60 Hz
            // source audit rather than applying one 2.0 rate to every clip.
            // Release/commit requests enter at their authored contact marker,
            // so slowing a long flourish does not delay the gameplay event.
            (int)EarthHumanoidPoseSlot.RaiseWall => RaiseWallMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.RaisePlatform => RaisePlatformMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.PullStone => PullStoneMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.HeavyThrow => HeavyThrowMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.VectorPush => VectorPushMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.GravityRepair => GravityRepairMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.WaveResonance => WaveResonanceMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.Pillar => PillarMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.ArmorAssemble => ArmorAssembleMaximumNormalizedSpeedPerSecond,
            (int)EarthHumanoidPoseSlot.ArmorBarrage => ArmorBarrageMaximumNormalizedSpeedPerSecond,
            // The saved Punching child has a dense authored forearm beat around
            // .36. This time-based calibration reduces the sampled source
            // interval. The .47 contact marker cannot be reached in less than
            // .31 seconds, and the final-pose frame-rate matrix validates the
            // resulting continuity and measured latency.
            (int)EarthHumanoidPoseSlot.GenericCast =>
                QuickPunchMaximumNormalizedSpeedPerSecond,
            _ => 0f
        };
    }
}
