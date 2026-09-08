using System;
using Elemental.Simulation.Bending;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.World
{
    [Serializable]
    public sealed class EarthMaterialEventTuning
    {
        public EarthMaterialFeedbackKind kind;
        [Tooltip("-1 preserves the event's size-dependent count; zero disables this layer.")]
        [Range(-1, 512)] public int dustCount = -1;
        [Range(-1, 128)] public int chipCount = -1;
        [Range(0f, 3f)] public float intensity = 1f;
        [Range(.1f, 4f)] public float particleSizeScale = 1f;
    }

    [Serializable]
    public sealed class EarthMaterialEventsTuning
    {
        [Min(8)] public int dustPerFrame = 256;
        [Min(8)] public int chipsPerFrame = 64;
        [Min(.1f)] public float mergeDistance = 1.5f;
        [Range(0f, 3f)] public float intensity = 1f;
        [Range(0f, 3f)] public float footsteps = 1f;
        [Range(0f, 3f)] public float emergence = 1f;
        [Range(0f, 3f)] public float repair = 1f;
        [Range(0f, 3f)] public float friction = 1f;
        public EarthMaterialEventTuning[] events = CreateDefaults();
        private static EarthMaterialEventTuning[] CreateDefaults()
        {
            var kinds = (EarthMaterialFeedbackKind[])Enum.GetValues(typeof(EarthMaterialFeedbackKind));
            var result = new EarthMaterialEventTuning[kinds.Length];
            for (int i = 0; i < kinds.Length; i++) result[i] = new EarthMaterialEventTuning { kind = kinds[i] };
            return result;
        }
        public EarthMaterialEventTuning For(EarthMaterialFeedbackKind kind)
        {
            if (events != null) for (int i = 0; i < events.Length; i++)
                if (events[i] != null && events[i].kind == kind) return events[i];
            return null;
        }
    }

    // Explicitly injected event fan-out. No static registry or gameplay mutation.
    [DefaultExecutionOrder(800)]
    public sealed class EarthMaterialFeedbackHub : MonoBehaviour
    {
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Elemental.Earth.MaterialFeedback");
        [SerializeField] private EarthEffectsTuningProfile effectsProfile;
        [SerializeField] private Transform prioritySubject;
        private readonly EarthMaterialFeedbackCue[] pending = new EarthMaterialFeedbackCue[8];
        private readonly EarthMaterialFeedbackCue[] surfacePending = new EarthMaterialFeedbackCue[384];
        private int surfaceCount, surfaceCursor;
        private readonly EarthMaterialEventsTuning defaults = new EarthMaterialEventsTuning();
        private int count;
        public event Action<EarthMaterialFeedbackCue> Presented;
        public int CoalescedEvents { get; private set; }
        public int DroppedEvents { get; private set; }
        public int BudgetClampedParticles { get; private set; }
        public int PresentedEvents { get; private set; }
        public int InvalidEvents { get; private set; }
        public void Configure(EarthEffectsTuningProfile profile, Transform subject) { effectsProfile = profile; prioritySubject = subject; }
        private EarthMaterialEventsTuning Tuning => effectsProfile != null ? effectsProfile.MaterialEvents : defaults;

        public void Emit(EarthMaterialFeedbackKind kind, Vector3 point, Vector3 normal, float strength = 1f,
            float radius = .35f, uint sourceId = 0, uint generation = 0, int dustCount = -1, int chipCount = -1)
        {
            if (!isActiveAndEnabled) return;
            if (!IsFinite(point) || !IsFinite(normal) || !float.IsFinite(strength) || !float.IsFinite(radius))
            { InvalidEvents++; return; }
            using (Marker.Auto())
            {
                var tuning = Tuning;
                var eventTuning = tuning.For(kind);
                float multiplier = kind == EarthMaterialFeedbackKind.Footstep || kind == EarthMaterialFeedbackKind.Roll ? tuning.footsteps :
                    kind == EarthMaterialFeedbackKind.Emerge || kind == EarthMaterialFeedbackKind.Assemble ? tuning.emergence :
                    kind == EarthMaterialFeedbackKind.RepairSeat || kind == EarthMaterialFeedbackKind.RepairComplete ? tuning.repair :
                    kind == EarthMaterialFeedbackKind.Friction ? tuning.friction : 1f;
                float safeStrength = Mathf.Clamp(strength, 0f, 3f);
                float gain = FiniteGain(tuning.intensity) * FiniteGain(multiplier) * safeStrength;
                float particleScale = 1f;
                if (eventTuning != null)
                {
                    gain *= FiniteGain(eventTuning.intensity);
                    if (eventTuning.dustCount >= 0) dustCount = eventTuning.dustCount;
                    if (eventTuning.chipCount >= 0) chipCount = eventTuning.chipCount;
                    particleScale = float.IsFinite(eventTuning.particleSizeScale) ? Mathf.Clamp(eventTuning.particleSizeScale, .1f, 4f) : 1f;
                }
                bool small = kind == EarthMaterialFeedbackKind.Footstep || kind == EarthMaterialFeedbackKind.Roll || kind == EarthMaterialFeedbackKind.Friction || kind == EarthMaterialFeedbackKind.RepairSeat;
                int dust = Mathf.Clamp(Mathf.RoundToInt((dustCount >= 0 ? dustCount : small ? 8 : 32) * gain), 0, 512);
                int chips = Mathf.Clamp(Mathf.RoundToInt((chipCount >= 0 ? chipCount : small ? 2 : 8) * gain), 0, 128);
                if (dust + chips == 0) return;
                var cue = new EarthMaterialFeedbackCue(kind, point, normal, safeStrength,
                    Mathf.Clamp(radius, .02f, 4f), sourceId, generation, dust, chips, particleScale);
                if (kind == EarthMaterialFeedbackKind.WaveSurfaceContact || kind == EarthMaterialFeedbackKind.WaveSurfaceBurst ||
                    kind == EarthMaterialFeedbackKind.ExtractionSurfaceContact)
                {
                    // Separate spatial contacts must survive the ordinary 1.5m impact
                    // merge. Still share the global per-frame particle budget.
                    if (surfaceCount < surfacePending.Length) surfacePending[surfaceCount++] = cue;
                    else DroppedEvents++;
                    return;
                }
                float mergeDistance = float.IsFinite(tuning.mergeDistance) ? Mathf.Clamp(tuning.mergeDistance, .1f, 8f) : 1.5f;
                float mergeSq = mergeDistance * mergeDistance;
                for (int i = 0; i < count; i++)
                {
                    if (pending[i].Kind != kind || math.distancesq(pending[i].Point, cue.Point) > mergeSq) continue;
                    pending[i] = pending[i].WithCounts(Mathf.Min(512, pending[i].DustCount + dust), Mathf.Min(128, pending[i].ChipCount + chips));
                    CoalescedEvents++; return;
                }
                if (count < pending.Length) { pending[count++] = cue; return; }
                // Keep actual contact locations, never average distant hits into empty space.
                if (prioritySubject != null)
                {
                    float3 origin = prioritySubject.position;
                    int farthest = 0;
                    for (int i = 1; i < count; i++)
                        if (math.distancesq(pending[i].Point, origin) > math.distancesq(pending[farthest].Point, origin)) farthest = i;
                    if (math.distancesq(cue.Point, origin) < math.distancesq(pending[farthest].Point, origin)) pending[farthest] = cue;
                }
                DroppedEvents++;
            }
        }

        private void LateUpdate() => FlushPending();

        public void FlushPending()
        {
            using (Marker.Auto())
            {
                int remainingDust = Mathf.Clamp(Tuning.dustPerFrame, 8, 4096), remainingChips = Mathf.Clamp(Tuning.chipsPerFrame, 8, 1024);
                for (int i = 0; i < count; i++)
                {
                    int remainingSlots = count - i - 1;
                    int dust = Mathf.Min(pending[i].DustCount, Mathf.Max(0, remainingDust - remainingSlots * 8));
                    int chips = Mathf.Min(pending[i].ChipCount, Mathf.Max(0, remainingChips - remainingSlots * 2));
                    BudgetClampedParticles += pending[i].DustCount + pending[i].ChipCount - dust - chips;
                    remainingDust -= dust; remainingChips -= chips;
                    Presented?.Invoke(pending[i].WithCounts(dust, chips)); PresentedEvents++;
                }
                count = 0;
                for (int i = 0; i < surfaceCount; i++)
                {
                    int index = (i + surfaceCursor) % surfaceCount;
                    var cue = surfacePending[index];
                    int slots = surfaceCount - i;
                    int dust = Mathf.Min(cue.DustCount, (remainingDust + slots - 1) / slots);
                    int chips = Mathf.Min(cue.ChipCount, (remainingChips + slots - 1) / slots);
                    remainingDust -= dust; remainingChips -= chips;
                    BudgetClampedParticles += cue.DustCount + cue.ChipCount - dust - chips;
                    if (dust + chips > 0) { Presented?.Invoke(cue.WithCounts(dust, chips)); PresentedEvents++; }
                }
                surfaceCursor = (surfaceCursor + 1) % surfacePending.Length; surfaceCount = 0;
            }
        }
        private void OnDisable() { count = 0; surfaceCount = 0; }
        private static float FiniteGain(float value) => float.IsFinite(value) ? Mathf.Clamp(value, 0f, 3f) : 0f;
        private static bool IsFinite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
