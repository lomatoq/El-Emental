using UnityEngine;
namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class EarthLandingSlamCollisionGrace : MonoBehaviour
    {
        private Collider _piece, _caster;
        private bool _wasIgnored;
        private float _until;
        public static void Apply(Collider piece, Collider caster)
        {
            if (piece == null || caster == null || piece == caster) return;
            var grace = piece.GetComponent<EarthLandingSlamCollisionGrace>();
            if (grace == null) grace = piece.gameObject.AddComponent<EarthLandingSlamCollisionGrace>();
            grace.Restore();
            grace._piece = piece; grace._caster = caster;
            grace._wasIgnored = UnityEngine.Physics.GetIgnoreCollision(piece, caster);
            UnityEngine.Physics.IgnoreCollision(piece, caster, true);
            grace._until = Time.fixedTime + .65f;
            grace.enabled = true;
        }
        private void FixedUpdate()
        { if (_piece == null || _caster == null || Time.fixedTime >= _until) enabled = false; }
        private void OnDisable() => Restore();
        private void Restore()
        {
            if (_piece != null && _caster != null)
                UnityEngine.Physics.IgnoreCollision(_piece, _caster, _wasIgnored);
            _piece = _caster = null;
        }
    }
}
