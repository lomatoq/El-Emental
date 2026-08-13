using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Input.Gestures
{
    [System.Flags]
    public enum EarthInputModifierFlags : byte
    {
        None = 0,
        Modifier = 1 << 0,
        Force = 1 << 1,
        Field = 1 << 2
    }

    /// <summary>
    /// Replay/network representation of resolved input. It contains quantized intent
    /// geometry and no raw device stream, screen pixels, camera references, or outcomes.
    /// </summary>
    public readonly struct EarthResolvedInputCommand
    {
        public EarthResolvedInputCommand(
            EarthIntentKind intent,
            uint sourceStableId,
            uint sourceGeneration,
            IReadOnlyList<uint2> quantizedGeometry,
            ushort chargeQ16,
            ushort wheelQ16,
            EarthInputModifierFlags modifiers,
            uint startTick,
            uint releaseTick,
            uint seed,
            uint gestureDigest)
        {
            Intent = intent;
            SourceStableId = sourceStableId;
            SourceGeneration = sourceGeneration;
            QuantizedGeometry = quantizedGeometry;
            ChargeQ16 = chargeQ16;
            WheelQ16 = wheelQ16;
            Modifiers = modifiers;
            StartTick = startTick;
            ReleaseTick = releaseTick;
            Seed = seed;
            GestureDigest = gestureDigest;
        }

        public EarthIntentKind Intent { get; }
        public uint SourceStableId { get; }
        public uint SourceGeneration { get; }
        public IReadOnlyList<uint2> QuantizedGeometry { get; }
        public ushort ChargeQ16 { get; }
        public ushort WheelQ16 { get; }
        public EarthInputModifierFlags Modifiers { get; }
        public uint StartTick { get; }
        public uint ReleaseTick { get; }
        public uint Seed { get; }
        public uint GestureDigest { get; }
    }

    public static class EarthInputCommandQuantizer
    {
        public static void QuantizeViewportGeometry(
            IReadOnlyList<PointerStrokeSample> samples,
            List<uint2> output,
            int maximumPoints = 32)
        {
            output.Clear();
            if (samples == null || samples.Count == 0) return;
            int count = math.clamp(maximumPoints, 2, 64);
            int outputCount = math.min(count, samples.Count);
            for (int index = 0; index < outputCount; index++)
            {
                int source = outputCount == 1
                    ? 0
                    : (int)math.round(index * (samples.Count - 1f) / (outputCount - 1f));
                float2 point = math.saturate(samples[source].ViewportPosition01);
                output.Add(new uint2(
                    (uint)math.round(point.x * 65535f),
                    (uint)math.round(point.y * 65535f)));
            }
        }

        public static ushort Quantize01(float value) =>
            (ushort)math.clamp(math.round(math.saturate(value) * 65535f), 0f, 65535f);
    }
}
