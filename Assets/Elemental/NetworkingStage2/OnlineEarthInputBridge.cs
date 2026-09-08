using Elemental.Input.Actions;
using Elemental.Simulation.Networking;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Existing host gesture/action/executor pipeline consumes semantic inputs unchanged.</summary>
    [DefaultExecutionOrder(-1500)]
    public sealed class OnlineEarthInputBridge : MonoBehaviour
    {
        [SerializeField] private EarthInputAdapter input;
        [SerializeField] private Camera castCamera;
        [SerializeField] private Rigidbody actorBody;
        private EarthOnlineGameplayBinding _binding;
        private NgoGameplayTransport _transport;
        private bool _client, _configured, _inputSuppressed;
        private EarthInputBits _lastSentHeld;
        private bool _previousEnabled;
        private float _nextSend;
        private uint _sequence;
        private bool _tapEligible, _predictedJump;
        private float _tapStarted, _tapThreshold, _predictedJumpExpires;
        public bool ConsumePredictedTapJump() { bool value = _predictedJump && Time.unscaledTime <= _predictedJumpExpires; _predictedJump = false; return value; }
        public Camera CastCamera => castCamera;
        public bool HasReferences => input != null && castCamera != null && actorBody != null;
        public void Configure(EarthOnlineGameplayBinding binding, NgoGameplayTransport transport, bool client, float tapJumpThreshold = .18f)
        {
            _previousEnabled = input.enabled;
            _binding = binding; _transport = transport; _client = client; _configured = true; _sequence = 0; _nextSend = 0;
            _tapEligible = _predictedJump = false; _inputSuppressed = false; _lastSentHeld = EarthInputBits.None;
            _tapThreshold = Mathf.Max(.05f, tapJumpThreshold);
            input.ConfigureRemoteInput(!client);
            input.enabled = true;
            input.RemoteFrameApplied -= ApplyCamera;
            if (!client) input.RemoteFrameApplied += ApplyCamera;
        }
        public void Stop()
        {
            if (_configured && input != null) { input.RemoteFrameApplied -= ApplyCamera; input.ConfigureRemoteInput(false); input.enabled = _previousEnabled; }
            _configured = false;
            _tapEligible = _predictedJump = false; _inputSuppressed = false; _lastSentHeld = EarthInputBits.None;
        }
        public void SetLocalInputSuppressed(bool suppressed)
        {
            if (_inputSuppressed == suppressed) return;
            _inputSuppressed = suppressed; _tapEligible = _predictedJump = false;
            if (suppressed && _configured && _client && _transport != null && _transport.Running)
                SendSuppressed(true);
            _nextSend = 0;
        }
        private void SendSuppressed(bool release)
        {
            var frame = new EarthSemanticInputFrame { Sequence = ++_sequence, Tick = _binding.AuthorityTick,
                PointerViewport = new float2(.5f, .5f), CameraPosition = (float3)castCamera.transform.position,
                CameraRotation = new quaternion(castCamera.transform.rotation.x, castCamera.transform.rotation.y,
                    castCamera.transform.rotation.z, castCamera.transform.rotation.w),
                FieldOfView = castCamera.fieldOfView, Aspect = castCamera.aspect };
            frame = EarthSemanticInputSuppression.Apply(frame, _lastSentHeld, release);
            _transport.Submit(Encode(frame)); _lastSentHeld = EarthInputBits.None;
            _nextSend = Time.unscaledTime + 1f / 30f;
        }
        private void Update()
        {
            if (!_configured || !_client || _transport == null || !_transport.Running) return;
            if (_inputSuppressed) { if (Time.unscaledTime >= _nextSend) SendSuppressed(false); return; }
            if (input.JumpPressed) { _tapStarted = Time.unscaledTime; _tapEligible = !input.BendModifierHeld && !input.BendFieldHeld; }
            if (input.BendModifierHeld || input.BendFieldHeld || input.CancelPressed) _tapEligible = false;
            if (input.JumpReleased)
            { _predictedJump |= _tapEligible && Time.unscaledTime - _tapStarted < _tapThreshold; _predictedJumpExpires = Time.unscaledTime + .2f; _tapEligible = false; }
            EarthInputBits held = EarthInputBits.None, pressed = EarthInputBits.None, released = EarthInputBits.None;
            Accumulate(EarthInputBits.Primary, input.BendPrimaryHeld, input.BendPrimaryPressed, input.BendPrimaryReleased, ref held, ref pressed, ref released);
            Accumulate(EarthInputBits.Force, input.BendForceHeld, input.BendForcePressed, input.BendForceReleased, ref held, ref pressed, ref released);
            Accumulate(EarthInputBits.Field, input.BendFieldHeld, input.BendFieldPressed, input.BendFieldReleased, ref held, ref pressed, ref released);
            Accumulate(EarthInputBits.Jump, input.JumpHeld, input.JumpPressed, input.JumpReleased, ref held, ref pressed, ref released);
            if (input.BendModifierHeld) held |= EarthInputBits.Modifier;
            if (input.WallPushModifierHeld) held |= EarthInputBits.WallPushModifier;
            if (input.CancelPressed) pressed |= EarthInputBits.Cancel;
            if (input.ShoulderSwapPressed) pressed |= EarthInputBits.Shoulder;
            for (int slot = 1; slot <= 4; slot++) if (input.DebugAbilityPressed(slot)) pressed |= (EarthInputBits)(64u << slot);
            float scroll = input.BendParameter;
            if (Time.unscaledTime < _nextSend && pressed == 0 && released == 0 && Mathf.Abs(scroll) < .001f) return;
            _nextSend = Time.unscaledTime + 1f / 30f;
            Vector2 pointer = input.PointerViewport01;
            var frame = new EarthSemanticInputFrame { Sequence = ++_sequence, Tick = _binding.AuthorityTick,
                Held = held, Pressed = pressed, Released = released,
                PointerViewport = new float2(Mathf.Clamp01(pointer.x), Mathf.Clamp01(pointer.y)),
                Move = new float2(input.Move.x, input.Move.y), Scroll = Mathf.Clamp(scroll, -240, 240),
                CameraPosition = (float3)castCamera.transform.position,
                CameraRotation = new quaternion(castCamera.transform.rotation.x, castCamera.transform.rotation.y,
                    castCamera.transform.rotation.z, castCamera.transform.rotation.w),
                FieldOfView = castCamera.fieldOfView, Aspect = castCamera.aspect };
            _transport.Submit(Encode(frame)); _lastSentHeld = held;
        }
        private static void Accumulate(EarthInputBits bit, bool h, bool p, bool r,
            ref EarthInputBits held, ref EarthInputBits pressed, ref EarthInputBits released)
        { if (h) held |= bit; if (p) pressed |= bit; if (r) released |= bit; }

        public bool Receive(in OnlinePacket packet)
        {
            if (!_configured || _client || packet.Kind != OnlineMessage.ControlIntent || packet.Aux != 1 || packet.Id != 2) return false;
            var frame = Decode(packet);
            if (!frame.Valid || math.distancesq(frame.CameraPosition, (float3)actorBody.position) > 400) return false;
            return input.EnqueueRemoteInput(frame);
        }
        private void ApplyCamera(EarthSemanticInputFrame frame)
        {
            castCamera.transform.SetPositionAndRotation((Vector3)frame.CameraPosition,
                new Quaternion(frame.CameraRotation.value.x, frame.CameraRotation.value.y, frame.CameraRotation.value.z, frame.CameraRotation.value.w));
            castCamera.fieldOfView = frame.FieldOfView; castCamera.aspect = frame.Aspect;
        }
        public static OnlinePacket Encode(in EarthSemanticInputFrame frame) => new OnlinePacket
        {
            Kind = OnlineMessage.ControlIntent, Aux = 1, Tick = frame.Tick, Seed = frame.Sequence, Flags = (uint)frame.Held,
            BuildHash = (uint)frame.Pressed | ((ulong)(uint)frame.Released << 32),
            A = new Vector3(frame.PointerViewport.x, frame.PointerViewport.y, frame.Scroll),
            B = (Vector3)frame.CameraPosition, C = new Vector3(frame.Move.x, frame.Move.y, frame.FieldOfView),
            D = new Vector3(frame.Aspect, 0, 0), Rotation = new Quaternion(frame.CameraRotation.value.x,
                frame.CameraRotation.value.y, frame.CameraRotation.value.z, frame.CameraRotation.value.w)
        };
        public static EarthSemanticInputFrame Decode(in OnlinePacket packet) => new EarthSemanticInputFrame
        {
            Sequence = packet.Seed, Tick = packet.Tick, Held = (EarthInputBits)packet.Flags,
            Pressed = (EarthInputBits)(uint)packet.BuildHash, Released = (EarthInputBits)(uint)(packet.BuildHash >> 32),
            PointerViewport = new float2(packet.A.x, packet.A.y), Scroll = packet.A.z,
            CameraPosition = (float3)packet.B, CameraRotation = new quaternion(packet.Rotation.x, packet.Rotation.y, packet.Rotation.z, packet.Rotation.w),
            Move = new float2(packet.C.x, packet.C.y), FieldOfView = packet.C.z, Aspect = packet.D.x
        };
    }
}


