using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using Elemental.Simulation.Matter;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Runtime.Matter
{
    [DisallowMultipleComponent]
    public sealed class EarthTechniqueComboRuntime : MonoBehaviour
    {
        private readonly EarthMoveHistory _history = new EarthMoveHistory(16);
        private readonly EarthComboOpportunity[] _opportunities = new EarthComboOpportunity[8];

        public EarthMoveHistory History => _history;
        public int OpportunityCount { get; private set; }
        public EarthComboOpportunity GetOpportunity(int index) =>
            index >= 0 && index < OpportunityCount ? _opportunities[index] : default;

        public void RecordAbility(
            AbilityId ability,
            EarthMatterId matter,
            uint tick,
            float energy,
            float3 direction)
        {
            EarthTechniqueId technique = Map(ability);
            if (technique == EarthTechniqueId.None) return;
            var record = new EarthMoveRecord(
                technique, matter, EarthEventTag.Formed, tick, tick, energy, direction);
            _history.Add(in record);
            Refresh(tick, matter);
        }

        public void RecordTechnique(
            EarthTechniqueId technique,
            EarthMatterId matter,
            EarthEventTag result,
            uint tick,
            float energy,
            Vector3 direction)
        {
            RecordResult(technique, matter, result, tick, tick, energy, direction);
        }

        public void RecordResult(
            EarthTechniqueId technique,
            EarthMatterId matter,
            EarthEventTag result,
            uint startTick,
            uint commitTick,
            float energy,
            Vector3 direction)
        {
            var record = new EarthMoveRecord(
                technique, matter, result, startTick, commitTick, energy,
                new float3(direction.x, direction.y, direction.z));
            _history.Add(in record);
            Refresh(commitTick, matter);
        }

        public void Refresh(uint tick, EarthMatterId activeMatter) =>
            OpportunityCount = EarthComboResolver.ResolveNonAlloc(
                _history, tick, activeMatter, _opportunities);

        private static EarthTechniqueId Map(AbilityId ability)
        {
            if (ability == EarthAbilityIds.LineWall) return EarthTechniqueId.RaiseWall;
            if (ability == EarthAbilityIds.PullRock) return EarthTechniqueId.PullStone;
            if (ability == EarthAbilityIds.FlickThrow) return EarthTechniqueId.ThrowStone;
            if (ability == EarthAbilityIds.RaisePlatform) return EarthTechniqueId.RaisePlatform;
            if (ability == EarthAbilityIds.VectorFieldPush) return EarthTechniqueId.VectorPush;
            if (ability == EarthAbilityIds.LandingCushion) return EarthTechniqueId.PillarJump;
            return EarthTechniqueId.None;
        }
    }
}
