using System;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class EarthMvpDuelController
    {
        [SerializeField] private GravityWorldBehaviour respawnGravityWorld;
        private static readonly ProfilerMarker RespawnReservationMarker = new("Elemental.Duel.RespawnReservation");
        private readonly RaycastHit[] _respawnGroundHits = new RaycastHit[64];
        private readonly Collider[] _respawnOverlaps = new Collider[64];
        private EarthRespawnCue _playerRespawnCue, _botRespawnCue;
        private uint _playerLifeGeneration, _botLifeGeneration, _respawnTick;
        private uint _playerRespawnRevision, _botRespawnRevision;
        private double _respawnClock, _playerActiveAt, _botActiveAt;
        private bool _playerReservationFailed, _botReservationFailed;
        public double RespawnPresentationTime => _respawnClock;
        public int RespawnReservationFailureCount { get; private set; }
        public bool RespawnPresentationConfigured => respawnGravityWorld != null;
        public event Action<EarthRespawnCue> RespawnCueChanged;
        public event Action<EarthRespawnCue> RespawnCompleted;
        public event Action<EarthDuelFighterId> RespawnCueCancelled;
        public event Action RespawnCuesCancelled;

        public void ConfigureRespawnPresentation(GravityWorldBehaviour gravity) =>
            respawnGravityWorld = gravity != null ? gravity : throw new ArgumentNullException(nameof(gravity));
        public bool TryGetRespawnCue(EarthDuelFighterId fighter, out EarthRespawnCue cue)
        { cue = fighter == EarthDuelFighterId.Player ? _playerRespawnCue : _botRespawnCue; return cue.IsValid; }
        private void BeginRespawnLife(EarthDuelFighterId fighter)
        {
            if (fighter == EarthDuelFighterId.Player)
            {
                _playerLifeGeneration = NextGeneration(_playerLifeGeneration); _playerRespawnCue = default;
                _playerActiveAt = _respawnClock + respawnSeconds; _playerReservationFailed = false; _playerRespawnRevision = 0;
            }
            else
            {
                _botLifeGeneration = NextGeneration(_botLifeGeneration); _botRespawnCue = default;
                _botActiveAt = _respawnClock + respawnSeconds; _botReservationFailed = false; _botRespawnRevision = 0;
            }
        }
        private static uint NextGeneration(uint value) => value == uint.MaxValue ? 1u : value + 1u;
        private void AdvanceRespawnClock(float deltaTime) { _respawnClock += deltaTime; _respawnTick++; }
        private void UpdateRespawnReservation(EarthDuelFighterId fighter, float remaining, bool respawnNow)
        {
            if (respawnGravityWorld == null || !respawnGravityWorld.IsReady ||
                (!respawnNow && (remaining <= 0 || remaining > GoldRespawnTimeline.TerminalSeconds))) return;
            using var marker = RespawnReservationMarker.Auto();
            bool player = fighter == EarthDuelFighterId.Player;
            EarthRespawnCue previous = player ? _playerRespawnCue : _botRespawnCue;
            if (!TryResolveRespawnPose(fighter, in previous, out Vector3 position, out Quaternion rotation,
                out Vector3 feet, out Vector3 up))
            {
                if (previous.IsValid)
                {
                    if (player) _playerRespawnCue = default; else _botRespawnCue = default;
                    RespawnCueCancelled?.Invoke(fighter);
                }
                bool reported = player ? _playerReservationFailed : _botReservationFailed;
                if (!reported)
                {
                    RespawnReservationFailureCount++;
                    Debug.LogError($"No clear supported respawn pose for {fighter} within the bounded spawn search. Inspect arena spawn clearance; gold reveal suppressed.", this);
                    if (player) _playerReservationFailed = true; else _botReservationFailed = true;
                }
                return;
            }
            bool changed = !previous.IsValid || Vector3.Distance(position, ToVector(previous.RootPosition)) > .01f ||
                Quaternion.Angle(rotation, ToRotation(previous.Rotation)) > .5f;
            if (!changed) return;
            double activeAt = player ? _playerActiveAt : _botActiveAt;
            // Initial cue uses the terminal interval; a displaced reservation restarts a shortened reveal.
            uint priorRevision = player ? _playerRespawnRevision : _botRespawnRevision;
            double startsAt = priorRevision > 0 ? Math.Min(_respawnClock, activeAt - .000001) :
                activeAt - GoldRespawnTimeline.TerminalSeconds;
            uint revision = NextGeneration(priorRevision);
            if (player) _playerRespawnRevision = revision; else _botRespawnRevision = revision;
            var cue = new EarthRespawnCue(fighter, player ? _playerLifeGeneration : _botLifeGeneration,
                revision,
                ToFloat(position), new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                ToFloat(feet), ToFloat(up), startsAt, activeAt);
            if (player) _playerRespawnCue = cue; else _botRespawnCue = cue;
            if (!respawnNow) RespawnCueChanged?.Invoke(cue);
        }
        private Vector3 ReservedRespawnPosition(EarthDuelFighterId fighter, Vector3 authored) =>
            TryGetRespawnCue(fighter, out EarthRespawnCue cue) ? ToVector(cue.RootPosition) : authored;
        private Quaternion ReservedRespawnRotation(EarthDuelFighterId fighter, Quaternion authored) =>
            TryGetRespawnCue(fighter, out EarthRespawnCue cue) ? ToRotation(cue.Rotation) : authored;
        private void CompleteRespawnLife(EarthDuelFighterId fighter)
        {
            TryGetRespawnCue(fighter, out EarthRespawnCue cue);
            if (fighter == EarthDuelFighterId.Player) _playerRespawnCue = default; else _botRespawnCue = default;
            if (cue.IsValid) RespawnCompleted?.Invoke(cue);
        }
        private void CancelRespawnPresentations()
        {
            _playerRespawnCue = _botRespawnCue = default;
            RespawnCuesCancelled?.Invoke();
        }
        private bool TryResolveRespawnPose(EarthDuelFighterId fighter, in EarthRespawnCue prior,
            out Vector3 position, out Quaternion rotation, out Vector3 feet, out Vector3 up)
        {
            bool player = fighter == EarthDuelFighterId.Player;
            Rigidbody body = player ? playerBody : botBody;
            CapsuleCollider capsule = body != null ? body.GetComponent<CapsuleCollider>() : null;
            position = feet = up = default; rotation = Quaternion.identity;
            if (capsule == null) return false;
            Vector3 authored = player ? _playerSpawnPosition : _botSpawnPosition;
            Quaternion authoredRotation = player ? _playerSpawnRotation : _botSpawnRotation;
            if (prior.IsValid && TryRespawnCandidate(body, capsule, ToVector(prior.RootPosition), authoredRotation,
                out position, out rotation, out feet, out up)) return true;
            Vector3 baseUp = ToVector(respawnGravityWorld.World.Sample(ToFloat(authored), _respawnTick).Up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(authoredRotation * Vector3.forward, baseUp).normalized;
            Vector3 right = Vector3.Cross(baseUp, forward);
            for (int candidate = 0; candidate < 17; candidate++)
            {
                float angle = (candidate - 1) * Mathf.PI * .25f;
                float radius = candidate == 0 ? 0 : candidate <= 8 ? 1.5f : 3f;
                Vector3 point = authored + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
                if (TryRespawnCandidate(body, capsule, point, authoredRotation, out position, out rotation, out feet, out up)) return true;
            }
            return false;
        }
        private bool TryRespawnCandidate(Rigidbody owner, CapsuleCollider capsule, Vector3 candidate,
            Quaternion authoredRotation, out Vector3 position, out Quaternion rotation, out Vector3 feet, out Vector3 up)
        {
            position = feet = default;
            up = ToVector(respawnGravityWorld.World.Sample(ToFloat(candidate), _respawnTick).Up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(authoredRotation * Vector3.forward, up).normalized;
            rotation = Quaternion.LookRotation(forward, up);
            var motor = owner.GetComponent<PlanetMotor>(); int mask = motor != null ? (int)motor.GroundMask : ~0;
            int count = UnityEngine.Physics.RaycastNonAlloc(candidate + up * 4, -up, _respawnGroundHits, 16, mask, QueryTriggerInteraction.Ignore);
            if (count == _respawnGroundHits.Length) return false;
            float nearest = float.PositiveInfinity; RaycastHit ground = default;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = _respawnGroundHits[index];
                if (hit.collider == null || IsCharacterCollider(hit.collider) || Vector3.Dot(hit.normal, up) < .65f ||
                    hit.rigidbody != null && !hit.rigidbody.isKinematic || hit.distance >= nearest) continue;
                nearest = hit.distance; ground = hit;
            }
            if (ground.collider == null) return false;
            Vector3 scale = owner.transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, capsule.height * Mathf.Abs(scale.y) * .5f);
            Vector3 centerOffset = rotation * Vector3.Scale(capsule.center, scale);
            feet = ground.point;
            position = feet + up * (halfHeight + .025f) - centerOffset;
            Vector3 center = position + centerOffset;
            float halfSegment = Mathf.Max(0, halfHeight - radius);
            int overlaps = UnityEngine.Physics.OverlapCapsuleNonAlloc(center + up * halfSegment, center - up * halfSegment,
                Mathf.Max(.02f, radius - .012f), _respawnOverlaps, mask, QueryTriggerInteraction.Ignore);
            if (overlaps == _respawnOverlaps.Length) return false;
            HumanoidRagdollRig ownRig = owner == playerBody ? playerHumanoidRagdoll : botHumanoidRagdoll;
            for (int index = 0; index < overlaps; index++)
            {
                Collider overlap = _respawnOverlaps[index];
                if (overlap == null || overlap.transform.IsChildOf(owner.transform) ||
                    ownRig != null && overlap.transform.IsChildOf(ownRig.transform)) continue;
                return false;
            }
            return true;
        }
        private bool IsCharacterCollider(Collider collider) =>
            playerBody != null && collider.transform.IsChildOf(playerBody.transform) ||
            botBody != null && collider.transform.IsChildOf(botBody.transform) ||
            playerHumanoidRagdoll != null && collider.transform.IsChildOf(playerHumanoidRagdoll.transform) ||
            botHumanoidRagdoll != null && collider.transform.IsChildOf(botHumanoidRagdoll.transform);
        private static float3 ToFloat(Vector3 value) => new(value.x, value.y, value.z);
        private static Vector3 ToVector(float3 value) => new(value.x, value.y, value.z);
        private static Quaternion ToRotation(quaternion value) => new(value.value.x, value.value.y, value.value.z, value.value.w);
    }
}
