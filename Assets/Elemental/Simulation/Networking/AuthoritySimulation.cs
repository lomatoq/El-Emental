using System;
using System.Collections.Generic;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Voxel;
using Unity.Mathematics;

namespace Elemental.Simulation.Networking
{
    public sealed class CommandAuthority
    {
        private readonly List<MagicCommand> _accepted = new List<MagicCommand>(256);
        private readonly List<CommandDecision> _decisions = new List<CommandDecision>(256);
        private readonly List<TerrainEditReplication> _terrain = new List<TerrainEditReplication>(256);
        private uint _nextSequence = 1u;
        private uint _lastTerrainSequence;

        public int AcceptedCount => _accepted.Count;
        public int DecisionCount => _decisions.Count;
        public int TerrainEditCount => _terrain.Count;
        public MagicCommand GetAccepted(int index) => _accepted[index];
        public CommandDecision GetDecision(int index) => _decisions[index];
        public TerrainEditReplication GetTerrainEdit(int index) => _terrain[index];

        public CommandDecision Submit(NetworkPeerId peer, in MagicCommand command, uint serverTick)
        {
            if (!peer.IsValid || command.CasterId != peer.Value)
                return Record(command.Tick, CommandDecisionKind.Rejected, serverTick, "Caster ownership mismatch.");
            if (command.Tick > serverTick + 12u || serverTick > command.Tick + 180u)
                return Record(command.Tick, CommandDecisionKind.Rejected, serverTick, "Command tick outside authority window.");
            if (command.Path.Length > 32 || !math.all(math.isfinite(command.Origin)) || !math.all(math.isfinite(command.Aim)))
                return Record(command.Tick, CommandDecisionKind.Rejected, serverTick, "Geometry constraints exceeded.");
            CommandDecisionKind kind = command.Tick == serverTick ? CommandDecisionKind.Accepted : CommandDecisionKind.Corrected;
            CommandDecision decision = Record(command.Tick, kind, serverTick, kind == CommandDecisionKind.Corrected ? "Retimed to authority tick." : string.Empty);
            _accepted.Add(command);
            return decision;
        }

        public bool ReplicateTerrain(uint authoritativeTick, in SdfEdit edit, uint chunkVersion, ulong chunkHash)
        {
            if (edit.Sequence <= _lastTerrainSequence) return false;
            _lastTerrainSequence = edit.Sequence;
            _terrain.Add(new TerrainEditReplication(_nextSequence++, authoritativeTick, edit, chunkVersion, chunkHash));
            return true;
        }

        private CommandDecision Record(uint commandTick, CommandDecisionKind kind, uint serverTick, string reason)
        {
            CommandDecision decision = new CommandDecision(_nextSequence++, commandTick, kind, serverTick, reason);
            _decisions.Add(decision);
            return decision;
        }
    }

    public readonly struct CorrectionResult
    {
        public CorrectionResult(float3 position, float3 velocity, bool snapped, float error)
        {
            Position = position; Velocity = velocity; Snapped = snapped; Error = error;
        }
        public float3 Position { get; }
        public float3 Velocity { get; }
        public bool Snapped { get; }
        public float Error { get; }
    }

    public static class PredictionReconciler
    {
        public static CorrectionResult Reconcile(float3 predictedPosition, float3 predictedVelocity, in RigidbodySnapshot authority, float softThreshold = 0.15f, float snapThreshold = 2f)
        {
            float error = math.distance(predictedPosition, authority.Position);
            if (error >= snapThreshold)
                return new CorrectionResult(authority.Position, authority.Velocity, true, error);
            if (error <= softThreshold)
                return new CorrectionResult(predictedPosition, predictedVelocity, false, error);
            float blend = math.saturate((error - softThreshold) / math.max(0.001f, snapThreshold - softThreshold));
            return new CorrectionResult(
                math.lerp(predictedPosition, authority.Position, 0.25f + (blend * 0.5f)),
                math.lerp(predictedVelocity, authority.Velocity, 0.35f), false, error);
        }
    }
}
