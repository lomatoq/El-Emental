using System;
using Elemental.Runtime.Characters;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Fire;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Fire
{
    /// <summary>One admitted hold, one group. Damage uses authority contact time; draining never damages.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class FireStreamSession : MonoBehaviour
    {
        public const float Range = 8f, Radius = .18f, DamagePerSecond = 16f, GrowSeconds = .12f;
        private static readonly ProfilerMarker Marker = new ProfilerMarker("Elemental.Fire.StreamAuthority");
        private FireWorldBehaviour world;
        private FireWorldImpact worldImpact;
        private Collider coverCollider;
        public void ConfigureWorldImpact(FireWorldImpact impact)=>worldImpact=impact;
        private EarthMvpDuelController duel;
        private EarthDuelFighterId owner, target;
        private Transform muzzle, ownerRoot;
        private PlanetMotor rivalMotor;
        private CapsuleCollider[] capsules;
        private Vector3[] previousA, previousB;
        private bool[] previousEnabled;
        private Vector2[] intervals;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly Collider[] overlaps = new Collider[32];
        private readonly FireFieldNode[] nodes = new FireFieldNode[1];
        private int mask = ~0;
        private Vector3 aim, previousMuzzle;
        private float age, cadence, pendingContact, previousLength;
        private bool localCapability, focused = true, stopping;
        private uint generation;
        public FireGroupHandle Group { get; private set; }
        public uint Generation => generation;
        public bool IsActive { get; private set; }
        public bool IsAvailable => enabled && localCapability && focused && world != null && world.IsReady &&
            duel != null && duel.CombatAllowed && duel.HasSimulationAuthority && duel.CanReceiveDamage(owner) &&
            !duel.IsRecoverablyKnockedDown(owner) && Time.timeScale > 0 && muzzle != null;
        public Vector3 AimPoint => aim;
        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position;
        public float CurrentLength { get; private set; }
        public float Power {get;private set;}=1;
        public float EffectiveRadius=>Radius*Mathf.Sqrt(Power);
        public float EffectiveRange=>Range*Mathf.Sqrt(Power);
        public void SetPower(float value){if(float.IsFinite(value))Power=Mathf.Clamp(value,.38f,2.5f);}
        public int QuerySaturations { get; private set; }
        public bool HasCoverContact { get; private set; }
        public Vector3 CoverPoint { get; private set; }
        public Vector3 CoverNormal { get; private set; }
        public float AppliedDamage { get; private set; }
        public event Action<uint, FireGroupHandle> Began;
        public event Action<uint> Ended;

        public void Configure(FireWorldBehaviour fireWorld, EarthMvpDuelController match,
            EarthDuelFighterId fighter, Transform handMuzzle, int collisionMask = ~0)
        {
            Stop();
            if (duel != null) duel.RoundRestarted -= OnRoundRestart;
            world = fireWorld != null ? fireWorld : throw new ArgumentNullException(nameof(fireWorld));
            duel = match != null ? match : throw new ArgumentNullException(nameof(match));
            muzzle = handMuzzle != null ? handMuzzle : throw new ArgumentNullException(nameof(handMuzzle));
            owner = fighter; target = fighter == EarthDuelFighterId.Player ? EarthDuelFighterId.Bot : EarthDuelFighterId.Player;
            ownerRoot = fighter == EarthDuelFighterId.Player ? duel.PlayerTransform : duel.BotTransform;
            Transform targetRoot = fighter == EarthDuelFighterId.Player ? duel.BotTransform : duel.PlayerTransform;
            if (ownerRoot == null || targetRoot == null) throw new ArgumentException("Bind both production fighter roots before Fire.");
            rivalMotor=targetRoot.GetComponentInChildren<PlanetMotor>(true);
            capsules = targetRoot.GetComponentsInChildren<CapsuleCollider>(true);
            if (capsules.Length == 0) throw new ArgumentException("Fire target requires its actual fighter capsule colliders.");
            previousA = new Vector3[capsules.Length]; previousB = new Vector3[capsules.Length];
            previousEnabled = new bool[capsules.Length]; intervals = new Vector2[capsules.Length];
            mask = collisionMask; duel.RoundRestarted += OnRoundRestart;
        }

        // Frontend binds this explicitly. Host authority by itself does not enable an unreplicated school.
        public void SetLocalCapability(bool available)
        { localCapability = available; if (!available) Stop(); }

        public bool TryBegin(Vector3 aimPoint)
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.FireBegin);
            if (stopping) return false;
            if (IsActive) return true;
            if (!IsAvailable || !Finite(aimPoint)) return false;
            aim = aimPoint; age = cadence = pendingContact = previousLength = CurrentLength = 0;
            previousMuzzle = MuzzlePosition;
            nodes[0] = MakeNode(previousMuzzle, Direction(), 0);
            uint next = unchecked(generation + 1); if (next == 0) next = 1;
            if (!world.TryCreate(unchecked(next * 747796405u + (uint)owner * 2891336453u), 1,
                    nodes[0], ownerRoot, out FireGroupHandle group)) return false;
            generation = next; Group = group; IsActive = true; CacheCapsules();
            Began?.Invoke(generation, group); return IsActive;
        }

        public void SetAim(Vector3 point) { if (Finite(point)) aim = point; }
        public void Stop()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.FireStop);
            if (!IsActive) return;
            FireGroupHandle stoppedGroup = Group; uint stoppedGeneration = generation;
            IsActive = false; stopping = true;
            try
            {
                // Retire this generation before synchronous damage/event callbacks can reenter.
                // A subsequent input edge may begin after Stop returns; callbacks cannot admit one.
                world.Stop(stoppedGroup); cadence = 0;
                FlushDamage();
                Ended?.Invoke(stoppedGeneration);
            }
            finally { pendingContact = 0; stopping = false; }
        }
        private void Update() { if (IsActive && !IsAvailable) Stop(); }
        private void FixedUpdate()
        {
            using var allocationScope = Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Measure(
                Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Path.FireAuthorityFixed);
            if (!IsActive) return;
            if (!IsAvailable) { Stop(); return; }
            using (Marker.Auto())
            {
                float dt = Time.fixedDeltaTime;
                Vector3 origin = MuzzlePosition, direction = Direction();
                age += dt;
                float requested = EffectiveRange * Mathf.Clamp01(age / GrowSeconds);
                CurrentLength = Clip(origin, direction, requested);
                if(HasCoverContact && worldImpact!=null)worldImpact.ApplyContact(coverCollider,CoverPoint,CoverNormal,direction,dt,Power);
                nodes[0] = MakeNode(origin, direction, CurrentLength);
                if (!world.TrySetNodes(Group, nodes, 1)) { Stop(); return; }
                if (duel.CanReceiveDamage(target))
                    pendingContact += ContactTime(origin, direction, Mathf.Min(previousLength, CurrentLength), CurrentLength, dt);
                CacheCapsules(); previousMuzzle = origin; previousLength = CurrentLength;
                cadence += dt;
                if (cadence + 1e-6f >= .1f) { cadence -= .1f; FlushDamage(); }
            }
        }

        private float ContactTime(Vector3 origin, Vector3 direction, float fromLength, float toLength, float dt)
        {
            int count = 0;
            for (int i = 0; i < capsules.Length; i++)
            {
                CapsuleCollider capsule = capsules[i];
                if (!Eligible(capsule)) continue;
                Capsule(capsule, out Vector3 a, out Vector3 b, out float radius);
                // Fixed-step translation is exact for the standing capsule. Articulated capsule
                // rotation is sampled at this authority step, never driven by render particles.
                Vector3 translation = previousEnabled[i] ? (a + b - previousA[i] - previousB[i]) * .5f : Vector3.zero;
                Vector3 relative = translation - (origin - previousMuzzle);
                if (!FireStreamContactInterval.TryGetInterval(previousMuzzle, direction, fromLength, toLength,
                    a - translation, b - translation, relative, radius + EffectiveRadius, out float entry, out float exit)) continue;
                int j = count++;
                while (j > 0 && intervals[j - 1].x > entry) { intervals[j] = intervals[j - 1]; j--; }
                intervals[j] = new Vector2(entry, exit);
            }
            // Union the compound rig before the one stable fighter ID receives damage.
            float seconds = 0, start = 0, end = 0;
            for (int i = 0; i < count; i++)
            {
                if (i == 0) { start = intervals[i].x; end = intervals[i].y; }
                else if (intervals[i].x <= end) end = Mathf.Max(end, intervals[i].y);
                else { seconds += end - start; start = intervals[i].x; end = intervals[i].y; }
            }
            if (count > 0) seconds += end - start;
            return Mathf.Clamp01(seconds) * dt;
        }

        private void FlushDamage()
        {
            float damage = pendingContact * DamagePerSecond * Power; pendingContact = 0;
            if (damage <= 0 || duel == null || !duel.HasSimulationAuthority || !duel.CombatAllowed ||
                !duel.CanReceiveDamage(owner) || duel.IsRecoverablyKnockedDown(owner) || !duel.CanReceiveDamage(target)) return;
            Vector3 shove=Direction()*Mathf.Min(3.5f,damage/DamagePerSecond*12f);
            rivalMotor?.ApplyFireImpulse(shove);
            if(worldImpact!=null)for(int i=0;i<capsules.Length;i++)if(capsules[i]!=null&&capsules[i].enabled&&capsules[i].gameObject.activeInHierarchy){Vector3 point=capsules[i].ClosestPoint(MuzzlePosition);worldImpact.ApplyContact(capsules[i],point,-Direction(),Direction(),Mathf.Min(.1f,damage/(DamagePerSecond*Power)),Power);break;}
            RagdollHandoff handoff = RagdollHandoff.Uniform(shove);
            duel.ApplyDamage(target, damage, in handoff); AppliedDamage += damage;
        }
        private float Clip(Vector3 origin, Vector3 direction, float length)
        {
            HasCoverContact = false; coverCollider=null;
            int overlapCount = UnityEngine.Physics.OverlapSphereNonAlloc(origin, EffectiveRadius, overlaps, mask, QueryTriggerInteraction.Ignore);
            if (overlapCount == overlaps.Length) { QuerySaturations++; return 0; }
            Collider touching=null; float nearestTouch=float.MaxValue;
            for(int i=0;i<overlapCount;i++)
            {
                var candidate=overlaps[i];if(Self(candidate))continue;
                // Unknown concave overlap blocks the stream, but cannot invent a scorch contact.
                if(!FireColliderSurface.TryPoint(candidate,origin,direction,EffectiveRadius+.02f,out Vector3 point))return 0;
                float squared=(point-origin).sqrMagnitude;
                if(squared>=nearestTouch)continue;nearestTouch=squared;touching=candidate;CoverPoint=point;
            }
            if(touching!=null)
            {
                Vector3 outward=origin-CoverPoint;CoverNormal=outward.sqrMagnitude>1e-6f?outward.normalized:-direction;
                Transform rival=target==EarthDuelFighterId.Player?duel.PlayerTransform:duel.BotTransform;
                HasCoverContact=rival==null||!touching.transform.IsChildOf(rival);
                coverCollider=HasCoverContact?touching:null;return 0;
            }
            int count = UnityEngine.Physics.SphereCastNonAlloc(origin, EffectiveRadius, direction, hits, length, mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) { QuerySaturations++; return 0; }
            Collider nearestCollider = null;
            for (int i = 0; i < count; i++)
            {
                if (Self(hits[i].collider) || hits[i].distance > length) continue;
                length = hits[i].distance; nearestCollider = hits[i].collider;
                CoverPoint = hits[i].point; CoverNormal = hits[i].normal;
            }
            Transform rivalRoot = target == EarthDuelFighterId.Player ? duel.PlayerTransform : duel.BotTransform;
            HasCoverContact = nearestCollider != null && rivalRoot != null &&
                !nearestCollider.transform.IsChildOf(rivalRoot) && CoverNormal.sqrMagnitude > .5f;
            coverCollider=HasCoverContact?nearestCollider:null;
            return Mathf.Max(0, length);
        }
        private bool Self(Collider collider) => collider == null || collider.transform.IsChildOf(ownerRoot);
        private Vector3 Direction()
        {
            Vector3 delta = aim - MuzzlePosition;
            return delta.sqrMagnitude > .000001f ? delta.normalized : ownerRoot.forward;
        }
        private FireFieldNode MakeNode(Vector3 origin, Vector3 direction, float length)
        {
            FireFieldNode node = FireFieldNode.Stream(origin, origin + direction * length, direction * 14f, ownerRoot.up);
            node.Radius = EffectiveRadius; node.Lift = .2f; node.Swirl = .55f; node.NoiseSpeed = .4f;
            return node;
        }
        private void CacheCapsules()
        {
            for (int i = 0; i < capsules.Length; i++)
            {
                previousEnabled[i] = Eligible(capsules[i]);
                if (previousEnabled[i]) Capsule(capsules[i], out previousA[i], out previousB[i], out _);
            }
        }
        private static bool Eligible(CapsuleCollider capsule) => capsule != null && capsule.enabled && capsule.gameObject.activeInHierarchy && !capsule.isTrigger;
        private static void Capsule(CapsuleCollider capsule, out Vector3 a, out Vector3 b, out float radius)
        {
            Vector3 scale = capsule.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            int axis = capsule.direction;
            radius = capsule.radius * Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
            float half = Mathf.Max(0, capsule.height * scale[axis] * .5f - radius);
            Vector3 localAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            Vector3 offset = capsule.transform.TransformDirection(localAxis).normalized * half;
            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            a = center - offset; b = center + offset;
        }
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private void OnRoundRestart()
        {
            // RoundRestarted is emitted after the new health state is installed.
            // Contact accumulated in the old round must never enter that state.
            pendingContact = 0; Stop();
        }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) Stop(); }
        private void OnApplicationPause(bool value) { if (value) Stop(); }
        private void OnDisable() => Stop();
        private void OnDestroy() { if (duel != null) duel.RoundRestarted -= OnRoundRestart; }
    }
}
