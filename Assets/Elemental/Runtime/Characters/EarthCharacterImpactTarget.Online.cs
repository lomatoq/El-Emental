using System;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Characters;
using Elemental.Simulation.Networking;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class EarthCharacterImpactTarget
    {
        public bool HasSimulationAuthority { get; private set; } = true;
        public EarthMvpDuelController BoundDuel => duelController;
        private uint _lastReplicaImpactSequence;
        public event Action<EarthResolvedImpactSnapshot> AuthorityImpactCommitted;
        public void ConfigureOnlineAuthority(bool authority)
        { HasSimulationAuthority = authority; _lastReplicaImpactSequence = 0; ClearDedupe(); }
        private void PublishAuthorityImpact(in EarthWorldResponseEvent response, float reaction, Vector3 rootDelta)
        {
            if (HasSimulationAuthority)
                AuthorityImpactCommitted?.Invoke(new EarthResolvedImpactSnapshot(response, reaction,
                    _motor != null ? _motor.ImpactStunRemaining : 0f, ToFloat3(rootDelta)));
        }
        public bool ApplyReplicaImpact(uint sequence, in EarthResolvedImpactSnapshot snapshot)
        {
            if (HasSimulationAuthority || sequence == 0 || sequence <= _lastReplicaImpactSequence ||
                snapshot.Response.TargetStableId != stableFighterId) return false;
            _lastReplicaImpactSequence = sequence;
            LastResponse = snapshot.Response.Response;
            LastReactionVelocityChange = snapshot.ReactionVelocity;
            _motor?.ApplyReplicaStun(snapshot.StunSeconds);
            if (EarthLocalizedPhysicsResponse.IsLocal(LastResponse))
                _visibleRagdoll?.ApplyLocalizedPhysicalResponse(snapshot.Response, snapshot.ReactionVelocity);
            // Full-ragdoll mode and recovery are driven by the authority fighter
            // phase snapshot. Root impulse is represented by the host body pose,
            // so this presentation entry never applies damage or a second shove.
            WorldResponseRequested?.Invoke(snapshot.Response);
            _worldResponseFanout?.Publish(snapshot.Response);
            ImpactResolved?.Invoke(LastResponse);
            return true;
        }
    }
}
