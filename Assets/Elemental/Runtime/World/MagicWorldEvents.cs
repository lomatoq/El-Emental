using System;
using Elemental.Simulation.Magic;

namespace Elemental.Runtime.World
{
    public sealed class MagicWorldEvents
    {
        public event Action<TerrainEditedEvent> TerrainEdited;
        public event Action<WallRaisedEvent> WallRaised;
        public event Action<WallCollapsedEvent> WallCollapsed;
        public event Action<FragmentSpawnedEvent> FragmentSpawned;
        public event Action<FragmentLaunchedEvent> FragmentLaunched;
        public event Action<EarthBodyGrabbedEvent> EarthBodyGrabbed;
        public event Action<EarthBodyReleasedEvent> EarthBodyReleased;
        public event Action<ImpactEvent> ImpactOccurred;
        public event Action<EarthImpactEvent> EarthImpactOccurred;
        public event Action<MeteorImpactEvent> MeteorImpactOccurred;
        public event Action<MagicPushEvent> MagicPushed;
        public event Action<AbilityRejectedEvent> AbilityRejected;
        public event Action<FieldSpawnedEvent> FieldSpawned;
        public event Action<PhaseChangedEvent> PhaseChanged;
        public event Action<ReactionTriggeredEvent> ReactionTriggered;

        public void Emit(in TerrainEditedEvent value)
        {
            TerrainEdited?.Invoke(value);
        }

        public void Emit(in WallRaisedEvent value)
        {
            WallRaised?.Invoke(value);
        }

        public void Emit(in WallCollapsedEvent value)
        {
            WallCollapsed?.Invoke(value);
        }

        public void Emit(in FragmentSpawnedEvent value)
        {
            FragmentSpawned?.Invoke(value);
        }

        public void Emit(in FragmentLaunchedEvent value)
        {
            FragmentLaunched?.Invoke(value);
        }

        public void Emit(in EarthBodyGrabbedEvent value)
        {
            EarthBodyGrabbed?.Invoke(value);
        }

        public void Emit(in EarthBodyReleasedEvent value)
        {
            EarthBodyReleased?.Invoke(value);
        }

        public void Emit(in ImpactEvent value)
        {
            ImpactOccurred?.Invoke(value);
        }

        public void Emit(in EarthImpactEvent value)
        {
            EarthImpactOccurred?.Invoke(value);
        }

        public void Emit(in MeteorImpactEvent value)
        {
            MeteorImpactOccurred?.Invoke(value);
        }

        public void Emit(in MagicPushEvent value)
        {
            MagicPushed?.Invoke(value);
        }

        public void Emit(in AbilityRejectedEvent value)
        {
            AbilityRejected?.Invoke(value);
        }

        public void Emit(in FieldSpawnedEvent value)
        {
            FieldSpawned?.Invoke(value);
        }

        public void Emit(in PhaseChangedEvent value)
        {
            PhaseChanged?.Invoke(value);
        }

        public void Emit(in ReactionTriggeredEvent value)
        {
            ReactionTriggered?.Invoke(value);
        }
    }
}
