using System;

namespace Elemental.Simulation.Bending
{
    public enum EarthQuickStoneBeat : byte { FirstPunch, SecondPunch, LeftKick, RightKick, SpinKick }

    /// <summary>One accepted paired-button command per beat; one pending command, never a spam queue.</summary>
    public sealed class EarthQuickStoneCombo
    {
        public const float ContinuationSeconds = .65f;
        private int _next;
        private float _lastFinishedAt = float.NegativeInfinity;
        private bool _buffered;
        public bool Active { get; private set; }
        public bool HasBufferedCommand => _buffered;
        public EarthQuickStoneBeat Beat { get; private set; }

        public bool TryBuffer()
        {
            if (!Active || _buffered) return false;
            _buffered = true;
            return true;
        }

        public bool TryBegin(float now, out EarthQuickStoneBeat beat)
        {
            beat = Beat;
            if (Active || float.IsNaN(now) || float.IsInfinity(now)) return false;
            if (now - _lastFinishedAt > ContinuationSeconds) _next = 0;
            Beat = beat = (EarthQuickStoneBeat)_next;
            Active = true;
            return true;
        }

        public void Complete(float now)
        {
            if (!Active) return;
            Active = false;
            _lastFinishedAt = now;
            _next = ((int)Beat + 1) % 5;
        }

        public bool ConsumeBuffered()
        {
            if (Active || !_buffered) return false;
            _buffered = false;
            return true;
        }

        public void Reset()
        {
            _next = 0;
            _lastFinishedAt = float.NegativeInfinity;
            Active = _buffered = false;
            Beat = default;
        }

        public static float ClipTime(EarthQuickStoneBeat beat, float elapsed)
        {
            float contact = ContactSeconds(beat);
            float marker = beat == EarthQuickStoneBeat.SpinKick ? .6f : IsKick(beat) ? .5f : .47f;
            if (elapsed <= contact) return Math.Clamp(elapsed / contact, 0f, 1f) * marker;
            return marker + Math.Clamp((elapsed - contact) / (Duration(beat) - contact), 0f, 1f) * (1f - marker);
        }

        public static bool IsKick(EarthQuickStoneBeat beat) => beat >= EarthQuickStoneBeat.LeftKick;
        public static float RadiusScale(EarthQuickStoneBeat beat) => beat == EarthQuickStoneBeat.SpinKick ? 1.3f : IsKick(beat) ? 1.15f : 1f;
        public static float Damage(EarthQuickStoneBeat beat) => beat == EarthQuickStoneBeat.SpinKick ? 18f : IsKick(beat) ? 12f : 8f;
        public static float ManaCost(EarthQuickStoneBeat beat) => beat == EarthQuickStoneBeat.SpinKick ? 20f : IsKick(beat) ? 10f : 4f;
        public static float Duration(EarthQuickStoneBeat beat) => beat == EarthQuickStoneBeat.SpinKick ? 1.10f : IsKick(beat) ? .80f : .81f;
        public static float ContactSeconds(EarthQuickStoneBeat beat) => beat == EarthQuickStoneBeat.SpinKick ? .66f : IsKick(beat) ? .40f : .53f;
        public static EarthTechniqueId Technique(EarthQuickStoneBeat beat) => beat switch
        {
            EarthQuickStoneBeat.LeftKick => EarthTechniqueId.QuickStoneLeftKick,
            EarthQuickStoneBeat.RightKick => EarthTechniqueId.QuickStoneRightKick,
            EarthQuickStoneBeat.SpinKick => EarthTechniqueId.QuickStoneSpinKick,
            _ => EarthTechniqueId.QuickStonePunch
        };
    }
}
