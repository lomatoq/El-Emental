using Elemental.Simulation.Characters;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Host actor-2 input source. Missing traffic cannot keep movement or jump held forever.</summary>
    public sealed class OnlineRemoteMotorInput : MonoBehaviour, IPlanetMotorInputSource
    {
        [SerializeField] private Transform aimFrame;
        private float2 _move;
        private bool _jump;
        private uint _lastJumpEdge;
        private float _receivedAt = float.NegativeInfinity;
        public void Configure(Transform frame)
        { aimFrame = frame; _move = default; _jump = false; _lastJumpEdge = 0; _receivedAt = float.NegativeInfinity; }
        public bool Receive(in OnlinePacket packet)
        {
            if (packet.Kind != OnlineMessage.MotorInput || packet.Id != 2 ||
                !math.all(math.isfinite(new float2(packet.A.x, packet.A.y))) ||
                new Vector2(packet.A.x, packet.A.y).sqrMagnitude > 1.01f || packet.B.sqrMagnitude < .9f ||
                packet.C.sqrMagnitude < .9f || Mathf.Abs(Vector3.Dot(packet.B.normalized, packet.C.normalized)) > .1f)
                return false;
            _move = new float2(packet.A.x, packet.A.y);
            if (packet.Aux > _lastJumpEdge) { _jump = true; _lastJumpEdge = packet.Aux; }
            if (aimFrame != null) aimFrame.rotation = Quaternion.LookRotation(packet.B, packet.C);
            _receivedAt = Time.unscaledTime; return true;
        }
        public PlanetMotorCommand SampleCommand(uint tick)
        {
            bool fresh = Time.unscaledTime - _receivedAt <= .2f;
            var result = new PlanetMotorCommand(tick, fresh ? _move : float2.zero, fresh && _jump);
            _jump = false; return result;
        }
        private void OnDisable() { _move = default; _jump = false; _lastJumpEdge = 0; _receivedAt = float.NegativeInfinity; }
    }
}
