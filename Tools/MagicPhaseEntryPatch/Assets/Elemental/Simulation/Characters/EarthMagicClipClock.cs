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
        public const float PullStoneMaximumNormalizedSpeedPerSecond = 0.75f;
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
            // PullStone is a 2.167s source. A 2.0 normalized/s clock plays it at
            // 4.3x. The measured .319 region crossed 58.7 degrees in one 30 Hz
            // frame even at 1.35 normalized/s; .75 limits source playback to
            // about 1.63x and reaches the .50 contact marker in .67s or later.
            (int)EarthHumanoidPoseSlot.PullStone =>
                PullStoneMaximumNormalizedSpeedPerSecond,
            // The saved Punching child has a dense authored forearm beat around
            // .36. This time-based calibration reduces the sampled source
            // interval. The .47 contact marker cannot be reached in less than
            // .31 seconds, and the final-pose frame-rate matrix validates the
            // resulting continuity and measured latency.
            (int)EarthHumanoidPoseSlot.GenericCast =>
                QuickPunchMaximumNormalizedSpeedPerSecond,
            _ => MaximumNormalizedSpeedPerSecond
        };
    }
}
