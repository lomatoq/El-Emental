using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>Fixed pair slots, independent of damage/repair contact rejection.</summary>
    public sealed class EarthContactFrictionFeedback
    {
        private readonly Collider[] _partners = new Collider[8];
        private readonly float[] _nextTimes = new float[8];
        private int _replacement;

        public void Emit(EarthMaterialFeedbackHub hub, Collision collision, uint sourceId, uint generation = 0)
        {
            if (hub == null || collision == null || collision.contactCount == 0 || collision.collider == null) return;
            IEarthPhysicalTarget other = collision.collider.GetComponentInParent<IEarthPhysicalTarget>();
            bool peerEmits = other is EarthFragment || other is EarthDestructibleDecorRock ||
                other is EarthArenaPiece || other is EarthPieceRuntime || other is EarthPlatformPiece ||
                other is EarthRockDebris || other is EarthPillarWaveColumn;
            if (peerEmits && other.StableEarthId != 0u && sourceId > other.StableEarthId) return;
            ContactPoint contact = collision.GetContact(0);
            float tangentSpeed = Vector3.ProjectOnPlane(collision.relativeVelocity, contact.normal).magnitude;
            if (tangentSpeed < 0.5f) return;
            int slot = -1;
            for (int i = 0; i < _partners.Length; i++)
                if (_partners[i] == collision.collider) { slot = i; break; }
            if (slot < 0)
            {
                slot = _replacement++ % _partners.Length;
                _partners[slot] = collision.collider;
                _nextTimes[slot] = 0f;
            }
            if (Time.fixedTime < _nextTimes[slot]) return;
            _nextTimes[slot] = Time.fixedTime + 0.08f;
            hub.Emit(EarthMaterialFeedbackKind.Friction, contact.point, contact.normal,
                Mathf.Clamp(tangentSpeed / 3f, 0.35f, 1f), 0.22f, sourceId, generation, 8, 2);
        }
    }
}
