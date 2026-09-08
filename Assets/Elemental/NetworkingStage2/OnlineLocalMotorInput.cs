using System;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Online
{
    /// <summary>Samples the authored input once per motor tick; client keeps normal local prediction.</summary>
    public sealed class OnlineLocalMotorInput : MonoBehaviour, IPlanetMotorInputSource
    {
        private IPlanetMotorInputSource _source;
        private EarthOnlineGameplayBinding _binding;
        private NgoGameplayTransport _transport;
        private Transform _frame;
        private bool _client;
        private uint _jumpEdge;
        private float _nextSend;
        private OnlineEarthInputBridge _earthInput;
        public void Configure(MonoBehaviour source, EarthOnlineGameplayBinding binding,
            NgoGameplayTransport transport, Transform frame, bool client, OnlineEarthInputBridge earthInput = null)
        {
            _source = source as IPlanetMotorInputSource ?? throw new ArgumentException("Actor input must implement IPlanetMotorInputSource.");
            _binding = binding; _transport = transport; _frame = frame; _client = client;
            _earthInput = earthInput;
            _jumpEdge = 0; _nextSend = 0;
        }
        public PlanetMotorCommand SampleCommand(uint tick)
        {
            PlanetMotorCommand sampled = _source.SampleCommand(tick);
            bool suppressed = _binding.LocalGameplayInputSuppressed;
            if (suppressed) sampled = new PlanetMotorCommand(tick, Unity.Mathematics.float2.zero, false);
            if (_client) sampled = new PlanetMotorCommand(tick, sampled.Move, !suppressed && _earthInput != null && _earthInput.ConsumePredictedTapJump());
            if (_client && _transport.Running)
            {
                if (sampled.JumpPressed) ++_jumpEdge;
                if (sampled.JumpPressed || Time.unscaledTime >= _nextSend)
                {
                    _nextSend = Time.unscaledTime + 1f / 30f;
                    _transport.Submit(new OnlinePacket { Kind = OnlineMessage.MotorInput,
                        Tick = _binding.AuthorityTick, Aux = _jumpEdge,
                        A = new Vector3(sampled.Move.x, sampled.Move.y, 0),
                        B = _frame.forward, C = _frame.up });
                }
            }
            return sampled;
        }
    }
}

