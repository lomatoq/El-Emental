using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public enum EarthRejectReason : byte
    {
        None = 0,
        MissingTargetCapability = 1,
        InvalidGrounding = 2,
        CameraUnreadable = 3,
        DiscontinuousMatter = 4,
        LowConfidence = 5
    }

    public struct EarthIntentCandidate
    {
        public EarthActionIntentKind Intent;
        public float Confidence;
        public float ContextScore;
        public float ContinuityScore;
        public float Score;
        public EarthRejectReason RejectReason;
    }

    public readonly struct EarthIntentContext
    {
        public EarthIntentContext(
            ushort targetCapabilities,
            bool grounded,
            bool cameraReadable,
            bool hasActiveMatter,
            uint activeMatterStableId,
            float preferenceBias = 1f)
        {
            TargetCapabilities = targetCapabilities;
            Grounded = grounded;
            CameraReadable = cameraReadable;
            HasActiveMatter = hasActiveMatter;
            ActiveMatterStableId = activeMatterStableId;
            PreferenceBias = math.clamp(preferenceBias, 0.5f, 1.5f);
        }
        public ushort TargetCapabilities { get; }
        public bool Grounded { get; }
        public bool CameraReadable { get; }
        public bool HasActiveMatter { get; }
        public uint ActiveMatterStableId { get; }
        public float PreferenceBias { get; }
    }

    public static class EarthRankedIntentResolver
    {
        public static int ResolveNonAlloc(
            in EarthGestureToken token,
            in EarthIntentContext context,
            EarthIntentCandidate[] destination)
        {
            if (destination == null || destination.Length == 0 || !token.IsValid) return 0;
            int count = 0;
            switch (token.Kind)
            {
                case EarthGestureTokenKind.Tap:
                case EarthGestureTokenKind.DoubleTap:
                    Add(ref count, destination, EarthActionIntentKind.QuickPrime, token.Confidence,
                        Capability(context, 1 << 0),
                        context.HasActiveMatter ? 0.38f : Continuity(token, context, 0.72f), context);
                    Add(ref count, destination, EarthActionIntentKind.FullBend, token.Confidence * 0.82f,
                        Capability(context, 1 << 4), Continuity(token, context, 1f), context);
                    break;
                case EarthGestureTokenKind.Flick:
                case EarthGestureTokenKind.PushToward:
                    Add(ref count, destination, EarthActionIntentKind.VectorFieldPush, token.Confidence,
                        Capability(context, 1 << 1), Continuity(token, context, 1f), context);
                    Add(ref count, destination, EarthActionIntentKind.QuickFire, token.Confidence * 0.9f,
                        Capability(context, 1 << 0), Continuity(token, context, 0.9f), context);
                    break;
                case EarthGestureTokenKind.CircleCW:
                    Add(ref count, destination, EarthActionIntentKind.Repair, token.Confidence,
                        Capability(context, 1 << 5), Continuity(token, context, 1f), context);
                    break;
                case EarthGestureTokenKind.CircleCCW:
                    Add(ref count, destination, EarthActionIntentKind.GravityField, token.Confidence,
                        Capability(context, 1 << 2), Continuity(token, context, 1f), context);
                    break;
                case EarthGestureTokenKind.DragLinear:
                case EarthGestureTokenKind.DragArc:
                case EarthGestureTokenKind.ClosedLoop:
                    Add(ref count, destination, EarthActionIntentKind.FullBend, token.Confidence,
                        context.Grounded ? 1f : 0f, Continuity(token, context, 0.82f), context);
                    break;
                case EarthGestureTokenKind.ScrollFlickDown:
                    Add(ref count, destination, EarthActionIntentKind.GravityField, token.Confidence,
                        Capability(context, 1 << 2), Continuity(token, context, 1f), context);
                    break;
                case EarthGestureTokenKind.DirectionReversal:
                    Add(ref count, destination, EarthActionIntentKind.ManipulateTarget, token.Confidence,
                        context.HasActiveMatter ? 1f : 0f, Continuity(token, context, 1.2f), context);
                    break;
            }
            Sort(destination, count);
            return count;
        }

        private static float Capability(in EarthIntentContext context, int bit) =>
            (context.TargetCapabilities & bit) != 0 ? 1f : 0f;

        private static float Continuity(
            in EarthGestureToken token,
            in EarthIntentContext context,
            float fallback)
        {
            if (!context.HasActiveMatter) return fallback;
            uint target = token.PointerDownTarget.IsValid
                ? token.PointerDownTarget.StableId
                : token.CommitTarget.StableId;
            return target != 0u && target == context.ActiveMatterStableId ? 1.35f : fallback * 0.7f;
        }

        private static void Add(
            ref int count,
            EarthIntentCandidate[] output,
            EarthActionIntentKind intent,
            float confidence,
            float contextScore,
            float continuity,
            in EarthIntentContext context)
        {
            if (count >= output.Length) return;
            EarthRejectReason reject = confidence < 0.35f
                ? EarthRejectReason.LowConfidence
                : contextScore <= 0f
                    ? EarthRejectReason.MissingTargetCapability
                    : !context.CameraReadable
                        ? EarthRejectReason.CameraUnreadable
                        : EarthRejectReason.None;
            float score = reject == EarthRejectReason.None
                ? math.saturate(confidence) * contextScore * continuity * context.PreferenceBias
                : 0f;
            output[count++] = new EarthIntentCandidate
            {
                Intent = intent,
                Confidence = math.saturate(confidence),
                ContextScore = contextScore,
                ContinuityScore = continuity,
                Score = score,
                RejectReason = reject
            };
        }

        private static void Sort(EarthIntentCandidate[] values, int count)
        {
            for (int index = 1; index < count; index++)
            {
                EarthIntentCandidate value = values[index];
                int cursor = index - 1;
                while (cursor >= 0 && values[cursor].Score < value.Score)
                {
                    values[cursor + 1] = values[cursor];
                    cursor--;
                }
                values[cursor + 1] = value;
            }
        }
    }
}
