using UnityEngine;

namespace Elemental.Presentation.Rendering
{
    public readonly struct CapsuleContactShadowDiagnosticsSnapshot
    {
        public CapsuleContactShadowDiagnosticsSnapshot(
            bool featureRequested,
            bool bufferUploaded,
            bool debugViewRendered,
            int frameIndex,
            int registeredCasterCount,
            int activeCasterCount,
            int uploadedProxyCount,
            int rejectedCasterCount,
            int rejectedProxyCount,
            int capacityRejectCount,
            int generationRejectCount,
            float maximumContactDistance)
        {
            FeatureRequested = featureRequested;
            BufferUploaded = bufferUploaded;
            DebugViewRendered = debugViewRendered;
            FrameIndex = frameIndex;
            RegisteredCasterCount = registeredCasterCount;
            ActiveCasterCount = activeCasterCount;
            UploadedProxyCount = uploadedProxyCount;
            RejectedCasterCount = rejectedCasterCount;
            RejectedProxyCount = rejectedProxyCount;
            CapacityRejectCount = capacityRejectCount;
            GenerationRejectCount = generationRejectCount;
            MaximumContactDistance = maximumContactDistance;
        }

        public bool FeatureRequested { get; }
        public bool BufferUploaded { get; }
        public bool DebugViewRendered { get; }
        public int FrameIndex { get; }
        public int RegisteredCasterCount { get; }
        public int ActiveCasterCount { get; }
        public int UploadedProxyCount { get; }
        public int RejectedCasterCount { get; }
        public int RejectedProxyCount { get; }
        public int CapacityRejectCount { get; }
        public int GenerationRejectCount { get; }
        public float MaximumContactDistance { get; }

        public CapsuleContactShadowDiagnosticsSnapshot WithUploadedBuffer()
        {
            return new CapsuleContactShadowDiagnosticsSnapshot(
                FeatureRequested,
                true,
                DebugViewRendered,
                FrameIndex,
                RegisteredCasterCount,
                ActiveCasterCount,
                UploadedProxyCount,
                RejectedCasterCount,
                RejectedProxyCount,
                CapacityRejectCount,
                GenerationRejectCount,
                MaximumContactDistance);
        }

        public CapsuleContactShadowDiagnosticsSnapshot WithRenderedDebugView()
        {
            return new CapsuleContactShadowDiagnosticsSnapshot(
                FeatureRequested,
                BufferUploaded,
                true,
                FrameIndex,
                RegisteredCasterCount,
                ActiveCasterCount,
                UploadedProxyCount,
                RejectedCasterCount,
                RejectedProxyCount,
                CapacityRejectCount,
                GenerationRejectCount,
                MaximumContactDistance);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CapsuleContactShadowDiagnostics : MonoBehaviour
    {
        private static CapsuleContactShadowDiagnosticsSnapshot s_Current;

        public static CapsuleContactShadowDiagnosticsSnapshot Current => s_Current;

        internal static void Publish(in CapsuleContactShadowDiagnosticsSnapshot snapshot)
        {
            s_Current = snapshot;
        }

        internal static void MarkBufferUploaded()
        {
            s_Current = s_Current.WithUploadedBuffer();
        }

        internal static void MarkDebugViewRendered()
        {
            s_Current = s_Current.WithRenderedDebugView();
        }

        internal static void PublishDisabled(bool requested)
        {
            CapsuleShadowBuffer buffer = CapsuleShadowBuffer.Shared;
            s_Current = new CapsuleContactShadowDiagnosticsSnapshot(
                requested,
                false,
                false,
                Time.frameCount,
                buffer.Count,
                0,
                0,
                0,
                0,
                buffer.CapacityRejectCount,
                buffer.GenerationRejectCount,
                0f);
        }
    }
}
