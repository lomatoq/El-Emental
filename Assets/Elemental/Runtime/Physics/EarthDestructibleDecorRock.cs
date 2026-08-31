using Unity.Profiling;
using UnityEngine;
using Elemental.Simulation.Structures;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class EarthDestructibleDecorRock : MonoBehaviour, IEarthPhysicalTarget
    {
        private static readonly ProfilerMarker ImpactMarker =
            new ProfilerMarker("Elemental.Earth.DecorRockImpact");

        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider shape;
        [SerializeField] private GravityBody gravityBody;
        [SerializeField] private EarthRockDebrisPool debrisPool;
        [SerializeField] private uint stableId;
        [SerializeField, Min(1f)] private float integrity = 720f;
        [SerializeField, Min(1f)] private float detachImpulse = 90f;
        [SerializeField, Min(1f)] private float shatterImpulse = 1250f;
        [SerializeField, Min(0.05f)] private float visualRadius = 0.8f;

        private uint _generation = 1u;
        private int _gripCount;
        private bool _anchored = true;
        private bool _shattered;

        public Rigidbody Body => body;
        public uint StableEarthId => stableId != 0u ? stableId : 0xD3000001u;
        public EarthPhysicalTargetHandle TargetHandle =>
            new EarthPhysicalTargetHandle(StableEarthId, _generation);
        public float EarthMass => body != null ? Mathf.Max(0.1f, body.mass) : 1f;
        public EarthPhysicalTargetKind TargetKind => EarthPhysicalTargetKind.Rock;
        public bool IsEarthTargetValid => !_shattered && gameObject.activeInHierarchy &&
                                          body != null && shape != null && shape.enabled;
        public bool IsAnchored => _anchored;
        public bool IsShattered => _shattered;

        public void Configure(
            uint configuredStableId,
            Rigidbody configuredBody,
            Collider configuredShape,
            GravityBody configuredGravity,
            EarthRockDebrisPool configuredDebrisPool,
            float configuredRadius,
            float configuredIntegrity)
        {
            stableId = configuredStableId != 0u ? configuredStableId : 0xD3000001u;
            body = configuredBody;
            shape = configuredShape;
            gravityBody = configuredGravity;
            debrisPool = configuredDebrisPool;
            visualRadius = Mathf.Max(0.05f, configuredRadius);
            integrity = Mathf.Max(1f, configuredIntegrity);
            Anchor();
        }

        public void ApplyImpact(Vector3 point, Vector3 direction, float impulse)
        {
            if (!IsEarthTargetValid || impulse <= 0f) return;
            using (ImpactMarker.Auto())
            {
                Vector3 safeDirection = direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : transform.up;
                EarthDecorRockDamageResult damage = EarthDecorRockDamageSolver.Resolve(
                    integrity,
                    impulse,
                    _anchored,
                    detachImpulse,
                    shatterImpulse);
                integrity = damage.Integrity;
                if (damage.Detach) Detach();
                if (!_anchored && body != null)
                {
                    float boundedImpulse = Mathf.Min(impulse, EarthMass * 12f);
                    body.AddForceAtPosition(safeDirection * boundedImpulse, point, ForceMode.Impulse);
                    body.WakeUp();
                }
                if (damage.Shatter)
                    Shatter(point, safeDirection);
            }
        }

        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)
        {
            _gripCount++;
            Detach();
        }

        public void OnEarthMagicReleased(EarthMagicGripKind grip)
        {
            _gripCount = Mathf.Max(0, _gripCount - 1);
            if (body != null) body.WakeUp();
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (shape == null) shape = GetComponent<Collider>();
            if (gravityBody == null) gravityBody = GetComponent<GravityBody>();
            if (debrisPool == null && Application.isPlaying)
                Debug.LogError("[Elemental] Destructible decor rock requires an authored debris pool.", this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_shattered || collision == null || collision.contactCount == 0) return;
            float impulse = collision.impulse.magnitude;
            if (impulse < detachImpulse) return;
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = collision.relativeVelocity.sqrMagnitude > 0.001f
                ? -collision.relativeVelocity.normalized
                : -contact.normal;
            ApplyImpact(contact.point, direction, impulse);
        }

        private void Anchor()
        {
            if (body == null) return;
            _anchored = true;
            // An anchored prop is authored world geometry until the first
            // impact/grab. Keeping a dynamic body with FreezeAll still lets the
            // physics depenetration solver move it on the first PlayMode frame,
            // which was the source of the visibly floating arena rocks.
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            if (gravityBody != null) gravityBody.enabled = false;
        }

        private void Detach()
        {
            if (!_anchored || body == null) return;
            _anchored = false;
            body.constraints = RigidbodyConstraints.None;
            body.isKinematic = false;
            body.detectCollisions = true;
            if (gravityBody != null) gravityBody.enabled = true;
            body.WakeUp();
        }

        private void Shatter(Vector3 point, Vector3 direction)
        {
            if (_shattered) return;
            _shattered = true;
            _generation++;
            Vector3 inherited = body != null ? body.linearVelocity : Vector3.zero;
            debrisPool?.EmitShatter(
                point,
                direction,
                inherited,
                visualRadius,
                EarthMass,
                StableEarthId ^ unchecked((uint)Time.frameCount));
            if (shape != null) shape.enabled = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
                renderers[index].enabled = false;
            if (body != null)
            {
                body.detectCollisions = false;
                body.isKinematic = true;
            }
        }
    }
}
