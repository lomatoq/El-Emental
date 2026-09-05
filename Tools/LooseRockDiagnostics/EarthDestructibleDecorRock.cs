using Unity.Profiling;
using UnityEngine;
using Elemental.Simulation.Structures;
using Elemental.Simulation.Bending;
using Elemental.Runtime.World;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Geometry;

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
        [SerializeField] private EarthMaterialFeedbackHub materialFeedback;
        [SerializeField] private EarthStoneBevelProfile stoneBevelProfile;
        private readonly EarthStoneRenderBevelCache _renderBevels = new();
        public void ConfigureMaterialFeedback(EarthMaterialFeedbackHub hub)
        { materialFeedback = hub; debrisPool?.ConfigureMaterialFeedback(hub); }
        public void ConfigureDebrisPool(EarthRockDebrisPool pool)
        { debrisPool = pool; debrisPool?.ConfigureMaterialFeedback(materialFeedback); }

        private uint _generation = 1u;
        private int _gripCount;
        private bool _anchored = true;
        private bool _shattered;
        private readonly Collider[] _initialOverlaps = new Collider[128];
        private int _initialOverlapCount;
        public int InitialOverlapProtectionCount => _initialOverlapCount;
        public int ProtectedInitialCollisionCount { get; private set; }
        public float LastProtectedCollisionImpulse { get; private set; }
        public float LastProtectedCollisionSeparation { get; private set; }
        public int CapturedInitialOverlapCount { get; private set; }
        public int ObservedCollisionCount { get; private set; }
        public Collider LastCollisionCollider { get; private set; }
        public float LastCollisionImpulse { get; private set; }
        public float LastCollisionApproach { get; private set; }
        public Vector3 LastCollisionRelativeVelocity { get; private set; }
        public Vector3 LastCollisionNormal { get; private set; }
        public float LastCollisionSeparation { get; private set; }
        public bool LastCollisionInitialProtected { get; private set; }
        public bool LastCollisionHadMagicOwner { get; private set; }
        private EarthMatterIdentity _matterIdentity;
        private readonly EarthContactFrictionFeedback _frictionFeedback = new();
        private void OnCollisionStay(Collision collision) =>
            _frictionFeedback.Emit(materialFeedback, collision, StableEarthId, _generation);

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
            ApplyUnifiedMass();
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
            if (_anchored) CaptureInitialOverlaps();
            _gripCount++;
            if (_anchored)
                materialFeedback?.Emit(EarthMaterialFeedbackKind.Extract, transform.position,
                    gravityBody != null && gravityBody.LastAcceleration.sqrMagnitude > 0.001f
                        ? -gravityBody.LastAcceleration.normalized : transform.up,
                    1f, visualRadius, StableEarthId, _generation, 192, 48);
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
            _matterIdentity = GetComponent<EarthMatterIdentity>();
            if (_matterIdentity == null) _matterIdentity = gameObject.AddComponent<EarthMatterIdentity>();
            MeshFilter[] visuals = GetComponentsInChildren<MeshFilter>(true);
            for (int index = 0; index < visuals.Length; index++)
            {
                Mesh source = visuals[index].sharedMesh;
                // Imported arena geometry already has authored edge treatment. Do
                // not infer a missing bevel from its name and replace user meshes.
                if (source == null || source.name.StartsWith("Arena_", System.StringComparison.Ordinal) ||
                    source.name.EndsWith("Bevel", System.StringComparison.Ordinal)) continue;
                visuals[index].sharedMesh = _renderBevels.Get(source, stoneBevelProfile);
            }
            ApplyUnifiedMass();
        }

        private void OnDestroy() => _renderBevels.Clear();

        private void Start()
        {
            // Runtime-authored decor is configured directly after AddComponent.
            // Resolve the scene pool once construction is complete.
            if (debrisPool == null)
                debrisPool = FindAnyObjectByType<EarthRockDebrisPool>();
            if (materialFeedback != null) debrisPool?.ConfigureMaterialFeedback(materialFeedback);
            debrisPool?.PrepareFracture(shape);
            if (debrisPool == null && Application.isPlaying)
                Debug.LogError(
                    $"[Elemental] Destructible decor rock '{name}' requires an authored debris pool.",
                    this);
        }

        private void ApplyUnifiedMass()
        {
            if (body == null) return;
            body.mass = EarthMatterMassRuntime.ResolveFromCollider(shape, visualRadius);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // A grabbed rock is being actively stabilized by the bending solver.
            // Depenetration impulses during the first detached frame are not a
            // deliberate throw and must not instantly shatter the held prop or
            // clear telekinesis before the player can move it.
            if (_shattered || collision == null || collision.contactCount == 0)
                return;
            ContactPoint contact = collision.GetContact(0);
            ObservedCollisionCount++;
            LastCollisionCollider = collision.collider;
            LastCollisionImpulse = collision.impulse.magnitude;
            LastCollisionRelativeVelocity = collision.relativeVelocity;
            LastCollisionNormal = contact.normal;
            // Unity reports the other body's velocity relative to this body.
            // Its contact normal points towards this body: positive dot closes.
            LastCollisionApproach = Mathf.Max(0f, Vector3.Dot(collision.relativeVelocity, contact.normal));
            LastCollisionSeparation = contact.separation;
            LastCollisionInitialProtected = false;
            LastCollisionHadMagicOwner = _gripCount > 0;
            for (int i = 0; i < _initialOverlapCount; i++)
                LastCollisionInitialProtected |= _initialOverlaps[i] == collision.collider;
            if (_gripCount > 0) return;
            // Only contacts penetrating the authored pose at magic detachment
            // are protected. Physics still resolves them normally; their solver
            // impulse is not a new impact. A new collider remains destructive.
            for (int i = 0; i < _initialOverlapCount; i++)
                if (_initialOverlaps[i] == collision.collider)
                {
                    ProtectedInitialCollisionCount++;
                    LastProtectedCollisionImpulse = collision.impulse.magnitude;
                    LastProtectedCollisionSeparation = collision.GetContact(0).separation;
                    return;
                }
            float approach = LastCollisionApproach;
            // Contact-offset/depenetration impulses can be large even when two
            // bodies are not approaching (including positive separation). Only
            // an actual closing contact may inflict collision damage. Explicit
            // ApplyImpact remains independent of this physical-contact guard.
            if (approach < .1f) return;
            float impulse = Mathf.Max(collision.impulse.magnitude, EarthMass * approach);
            bool breakAttempted = false;
            if (!_anchored && debrisPool != null)
            {
                EarthRockBreakDecision decision = debrisPool.ResolveBreak(visualRadius, EarthMass, impulse);
                breakAttempted = decision.Breaks;
                if (decision.Breaks && debrisPool.TryEmitBreak(contact.point, contact.normal,
                    body.linearVelocity, visualRadius, EarthMass, StableEarthId, decision, 0, _matterIdentity))
                {
                    HideShattered();
                    return;
                }
            }
            if (impulse >= detachImpulse)
            {
                Vector3 direction = collision.relativeVelocity.sqrMagnitude > 0.001f
                    ? -collision.relativeVelocity.normalized : -contact.normal;
                ApplyImpact(contact.point, direction, impulse);
            }
            if (!_shattered && !breakAttempted && approach >= 0.75f)
                materialFeedback?.Emit(EarthMaterialFeedbackKind.Impact, contact.point, contact.normal,
                    Mathf.Clamp(approach / 8f, 0.4f, 1f), visualRadius, StableEarthId, _generation);
        }

        private void Anchor()
        {
            if (body == null) return;
            System.Array.Clear(_initialOverlaps, 0, _initialOverlaps.Length);
            _initialOverlapCount = 0;
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

        private void CaptureInitialOverlaps()
        {
            if (shape == null || !shape.enabled) return;
            Bounds bounds = shape.bounds;
            int count = UnityEngine.Physics.OverlapBoxNonAlloc(bounds.center,
                bounds.extents + Vector3.one * shape.contactOffset, _initialOverlaps,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == _initialOverlaps.Length)
                Debug.LogWarning($"[Elemental] Initial rock overlap query saturated for '{name}'; inspect collider density around this authored stone.", this);
            _initialOverlapCount = 0;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = _initialOverlaps[i];
                if (candidate == null || candidate == shape || candidate.attachedRigidbody == body ||
                    UnityEngine.Physics.GetIgnoreLayerCollision(shape.gameObject.layer, candidate.gameObject.layer) ||
                    UnityEngine.Physics.GetIgnoreCollision(shape, candidate) || !Penetrates(candidate)) continue;
                _initialOverlaps[_initialOverlapCount++] = candidate;
            }
            System.Array.Clear(_initialOverlaps, _initialOverlapCount, _initialOverlaps.Length - _initialOverlapCount);
            CapturedInitialOverlapCount = _initialOverlapCount;
        }

        private bool Penetrates(Collider other) => other != null && other.enabled &&
            other.gameObject.activeInHierarchy && UnityEngine.Physics.ComputePenetration(
                shape, shape.transform.position, shape.transform.rotation,
                other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > 0;

        private void FixedUpdate()
        {
            // Check before simulation: collision callbacks arrive after the solver
            // may already have removed the initial penetration in this same step.
            for (int i = _initialOverlapCount - 1; i >= 0; i--)
                if (!Penetrates(_initialOverlaps[i])) RemoveInitialOverlap(i);
        }

        private void OnCollisionExit(Collision collision)
        {
            for (int i = _initialOverlapCount - 1; i >= 0; i--)
                if (_initialOverlaps[i] == collision.collider) RemoveInitialOverlap(i);
        }

        private void RemoveInitialOverlap(int index)
        {
            _initialOverlaps[index] = _initialOverlaps[--_initialOverlapCount];
            _initialOverlaps[_initialOverlapCount] = null;
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
            Vector3 inherited = body != null ? body.linearVelocity : Vector3.zero;
            EarthRockBreakDecision decision = debrisPool != null ? debrisPool.ResolveBreak(visualRadius, EarthMass,
                Mathf.Max(100000f, EarthMass * 100f)) : default;
            if (debrisPool == null || !debrisPool.TryEmitBreak(
                point,
                direction,
                inherited,
                visualRadius,
                EarthMass,
                StableEarthId, decision, 0, _matterIdentity)) return;
            HideShattered();
        }

        private void HideShattered()
        {
            _shattered = true;
            _generation++;
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
