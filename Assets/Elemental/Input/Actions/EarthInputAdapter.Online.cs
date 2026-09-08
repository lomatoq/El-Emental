using Elemental.Simulation.Networking;
using UnityEngine;

namespace Elemental.Input.Actions
{
    public sealed partial class EarthInputAdapter
    {
        private readonly EarthSemanticInputFrame[] _remoteFrames = new EarthSemanticInputFrame[64];
        private EarthSemanticInputFrame _remoteFrame;
        private int _remoteHead, _remoteCount, _remoteEdgeFrame = -1;
        private uint _remoteSequence;
        private float _remoteLastTraffic = float.NegativeInfinity;
        private bool _remoteExpired;
        private bool _remoteInputEnabled;
        public bool GameplayInputSuppressed { get; private set; }
        // A paused local adapter reads an empty semantic frame instead of hardware.
        // Keeping the override independent of enabled state survives ragdoll recovery.
        public bool RemoteInputEnabled => _remoteInputEnabled || GameplayInputSuppressed;
        public void SetGameplayInputSuppressed(bool suppressed)
        {
            if (GameplayInputSuppressed == suppressed) return;
            GameplayInputSuppressed = suppressed;
            if (suppressed)
            {
                Unbind(); _remoteFrame = default; _remoteFrame.Pressed = EarthInputBits.Cancel;
                _remoteEdgeFrame = Time.frameCount;
                JumpCanceled?.Invoke();
            }
            else if (!_remoteInputEnabled && isActiveAndEnabled) Bind();
        }
        public event System.Action<EarthSemanticInputFrame> RemoteFrameApplied;
        public void ConfigureRemoteInput(bool enabled)
        {
            _remoteInputEnabled = enabled; GameplayInputSuppressed = false; _remoteHead = _remoteCount = 0;
            _remoteSequence = 0; _remoteFrame = default; _remoteEdgeFrame = -1;
            _remoteLastTraffic = float.NegativeInfinity; _remoteExpired = false;
            if (enabled) Unbind(); else if (isActiveAndEnabled) Bind();
        }
        public bool EnqueueRemoteInput(in EarthSemanticInputFrame frame)
        {
            if (!RemoteInputEnabled || !frame.Valid || frame.Sequence <= _remoteSequence || _remoteCount == _remoteFrames.Length) return false;
            _remoteFrames[(_remoteHead + _remoteCount) % _remoteFrames.Length] = frame;
            ++_remoteCount; _remoteSequence = frame.Sequence; _remoteLastTraffic = Time.unscaledTime; _remoteExpired = false;
            return true;
        }
        private void AdvanceRemoteInput()
        {
            if (GameplayInputSuppressed) return;
            if (Time.unscaledTime - _remoteLastTraffic > .25f)
            {
                if (!_remoteExpired)
                {
                    _remoteFrame = default; _remoteFrame.Pressed = EarthInputBits.Cancel;
                    _remoteCount = 0; _remoteEdgeFrame = Time.frameCount; _remoteExpired = true;
                }
                return;
            }
            if (_remoteCount == 0) return;
            // Coalesce stale continuous samples after a host frame hitch. Preserve
            // every press/release/scroll edge on a separate routed frame.
            while (_remoteCount > 1)
            {
                EarthSemanticInputFrame queued = _remoteFrames[_remoteHead];
                if (queued.Pressed != 0 || queued.Released != 0 || Mathf.Abs(queued.Scroll) > .001f) break;
                _remoteHead = (_remoteHead + 1) % _remoteFrames.Length; --_remoteCount;
            }
            _remoteFrame = _remoteFrames[_remoteHead]; _remoteHead = (_remoteHead + 1) % _remoteFrames.Length; --_remoteCount;
            _remoteEdgeFrame = Time.frameCount;
            RemoteFrameApplied?.Invoke(_remoteFrame);
            if (RemotePressed(EarthInputBits.Jump)) { JumpStarted?.Invoke(); JumpPerformed?.Invoke(); }
            if (RemoteReleased(EarthInputBits.Jump)) JumpCanceled?.Invoke();
        }
        private bool RemoteHeld(EarthInputBits bit) => (_remoteFrame.Held & bit) != 0;
        private bool RemotePressed(EarthInputBits bit) => _remoteEdgeFrame == Time.frameCount && (_remoteFrame.Pressed & bit) != 0;
        private bool RemoteReleased(EarthInputBits bit) => _remoteEdgeFrame == Time.frameCount && (_remoteFrame.Released & bit) != 0;
        private Vector2 RemotePointerPixels => ViewportToScreen(new Vector2(_remoteFrame.PointerViewport.x, _remoteFrame.PointerViewport.y));
        private float RemoteScroll => _remoteEdgeFrame == Time.frameCount ? _remoteFrame.Scroll : 0;
    }
}


