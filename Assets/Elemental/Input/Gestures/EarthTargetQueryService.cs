using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using UnityEngine;

namespace Elemental.Input.Gestures
{
    public readonly struct EarthTargetQueryHit
    {
        public EarthTargetQueryHit(in RaycastHit hit, in EarthResolvedTarget target)
        {
            Hit = hit;
            Target = target;
        }

        public RaycastHit Hit { get; }
        public EarthResolvedTarget Target { get; }
        public bool IsValid => Hit.collider != null && Target.IsValid;
    }

    /// <summary>
    /// One bounded query path for LMB/MMB/RMB. Exact ray hits win; the assist
    /// sphere is only a fallback and never replaces the target after press.
    /// </summary>
    public sealed class EarthTargetQueryService
    {
        private const int HitCapacity = 32;
        private readonly RaycastHit[] _hits = new RaycastHit[HitCapacity];

        public bool TryQuery(
            Ray ray,
            float maximumDistance,
            float assistRadius,
            Collider planetCollider,
            Rigidbody casterBody,
            EarthTargetCapabilities requiredAny,
            out EarthTargetQueryHit result)
        {
            int count = Physics.RaycastNonAlloc(
                ray, _hits, maximumDistance, ~0, QueryTriggerInteraction.Ignore);
            if (TrySelect(count, planetCollider, casterBody, requiredAny, out result)) return true;
            if (assistRadius <= 0f)
            {
                result = default;
                return false;
            }
            count = Physics.SphereCastNonAlloc(
                ray, assistRadius, _hits, maximumDistance, ~0, QueryTriggerInteraction.Ignore);
            return TrySelect(count, planetCollider, casterBody, requiredAny, out result);
        }

        private bool TrySelect(
            int hitCount,
            Collider planetCollider,
            Rigidbody casterBody,
            EarthTargetCapabilities requiredAny,
            out EarthTargetQueryHit result)
        {
            result = default;
            float nearest = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _hits[index];
                if (hit.collider == null || hit.distance >= nearest || IsCaster(hit.collider, casterBody)) continue;
                EarthResolvedTarget target = EarthTargetResolver.Resolve(hit.collider, planetCollider);
                if (!target.IsValid || (requiredAny != EarthTargetCapabilities.None &&
                    (target.Capabilities & requiredAny) == EarthTargetCapabilities.None)) continue;
                nearest = hit.distance;
                result = new EarthTargetQueryHit(in hit, in target);
            }
            return result.IsValid;
        }

        private static bool IsCaster(Collider candidate, Rigidbody casterBody)
        {
            if (candidate == null || casterBody == null) return false;
            if (candidate.attachedRigidbody == casterBody ||
                candidate.transform == casterBody.transform ||
                candidate.transform.IsChildOf(casterBody.transform)) return true;
            ActiveRagdollPuppet puppet = casterBody.GetComponent<ActiveRagdollPuppet>();
            if (puppet != null && puppet.OwnsCollider(candidate)) return true;
            PlanetMotor motor = casterBody.GetComponent<PlanetMotor>();
            return motor != null && candidate.GetComponentInParent<PlanetMotor>() == motor;
        }
    }
}
