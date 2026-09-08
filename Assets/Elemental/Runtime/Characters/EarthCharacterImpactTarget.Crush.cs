using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    public sealed partial class EarthCharacterImpactTarget
    {
        private struct LoadContact
        {
            public Rigidbody Source, ReceiverBody;
            public Collider Receiver;
            public Collider Stone;
            public Vector3 StonePoint, ReceiverPoint;
            public float Force, Time;
            public EarthSettledLoad Settled;
        }
        private readonly LoadContact[] _loads = new LoadContact[64];
        private float _loadDwell, _pinnedDwell, _nextPinClearanceProbe;
        private static readonly ProfilerMarker CrushMarker = new("Elemental.Character.SustainedCrush");
        public bool IsUnderCrushingLoad { get; private set; }
        // Hold an existing physical body during the load qualification window.
        // Otherwise authored recovery erases contacts before .2s can accumulate.
        public bool HasBlockingCrushContact => _loadDwell > 0f;
        public float SustainedLoadNewtons { get; private set; }
        public bool IsPinnedByStoneLoad => _pinnedDwell >= EarthSustainedCrush.PinnedDwellSeconds;
        public float PinnedLoadSeconds => _pinnedDwell;

        // Called by the stone's collision adapter; normal points out of the receiver.
        public void RecordStoneLoad(Collision collision, Rigidbody source)
        {
            if (!HasSimulationAuthority || source == null || source.isKinematic || collision == null) return;
            EarthFragment fragment = source.GetComponent<EarthFragment>();
            if (fragment != null && fragment.IsHeld) return;
            Vector3 up = _motor != null ? _motor.LocalUp : transform.up;
            for (int c = 0; c < collision.contactCount; c++)
            {
                ContactPoint contact = collision.GetContact(c);
                Collider receiver = contact.otherCollider;
                Collider stone = contact.thisCollider;
                if (receiver == null || stone == null || EarthStoneCharacterContact.ResolveTarget(receiver) != this) continue;
                float force = EarthSustainedCrush.OverheadContactForce(ToFloat3(collision.impulse),
                    ToFloat3(contact.normal), ToFloat3(up), Vector3.Dot(source.worldCenterOfMass - contact.point, up),
                    Time.fixedDeltaTime);
                if (force <= 0f) continue;
                int slot = -1;
                for (int i = 0; i < _loads.Length; i++)
                {
                    if (_loads[i].Source == source && _loads[i].ReceiverBody == receiver.attachedRigidbody &&
                        (receiver.attachedRigidbody != null || _loads[i].Receiver == receiver)) { slot = i; break; }
                    if (slot < 0 && (_loads[i].Source == null || Time.time - _loads[i].Time > .12f && !_loads[i].Source.IsSleeping())) slot = i;
                }
                if (slot < 0) return;
                // PhysX reports impulse per body pair. Sample once per physics tick;
                // five slow, consistent contacts exclude landing impulse spikes.
                LoadContact previous = _loads[slot];
                bool samePair = previous.Source == source && previous.ReceiverBody == receiver.attachedRigidbody &&
                    (receiver.attachedRigidbody != null || previous.Receiver == receiver);
                bool duplicate = samePair && previous.Time == Time.time;
                if (duplicate) force = Mathf.Max(force, previous.Force);
                EarthSettledLoad settled = duplicate ? previous.Settled : EarthSustainedCrush.SampleSettledLoad(
                    previous.Settled, force, collision.relativeVelocity.magnitude,
                    samePair && Time.time - previous.Time <= Time.fixedDeltaTime * 1.5f);
                _loads[slot] = new LoadContact { Source = source, ReceiverBody = receiver.attachedRigidbody, Stone = stone, Receiver = receiver,
                    StonePoint = stone.transform.InverseTransformPoint(contact.point),
                    ReceiverPoint = receiver.transform.InverseTransformPoint(contact.point), Force = force, Time = Time.time, Settled = settled };
                return;
            }
        }

        private void StepSustainedCrush()
        {
            using (CrushMarker.Auto())
            {
                SustainedLoadNewtons = 0f;
                if (targetBody == null || !HasSimulationAuthority || Time.time < _suppressUntil ||
                    duelController != null && !duelController.CanReceiveDamage(fighterId))
                { ClearCrushingLoad(); return; }
                for (int i = 0; i < _loads.Length; i++)
                {
                    LoadContact contact = _loads[i];
                    bool valid = contact.Source != null && !contact.Source.isKinematic && contact.Stone != null &&
                        contact.Stone.enabled && contact.Receiver != null && contact.Receiver.enabled &&
                        contact.Stone.gameObject.activeInHierarchy && contact.Receiver.gameObject.activeInHierarchy;
                    if (valid && Time.time - contact.Time > .12f)
                        valid = contact.Source.IsSleeping() && Vector3.SqrMagnitude(
                            contact.Stone.transform.TransformPoint(contact.StonePoint) -
                            contact.Receiver.transform.TransformPoint(contact.ReceiverPoint)) < .0025f;
                    if (!valid) { _loads[i] = default; continue; }
                }
                for (int i = 0; i < _loads.Length; i++)
                {
                    LoadContact contact = _loads[i];
                    if (contact.Source == null) continue;
                    float force = contact.Force;
                    if (Time.time - contact.Time > .12f)
                    {
                        // Sleeping bodies emit no Stay callbacks. A settled measured
                        // force retains upper stones transmitted through this body.
                        // Without five stable samples, conservatively cap old impulse.
                        if (contact.Settled.RetainedForce > 0f)
                        {
                            SustainedLoadNewtons += contact.Settled.RetainedForce;
                            continue;
                        }
                        float totalSourceForce = 0f;
                        for (int j = 0; j < _loads.Length; j++)
                            if (_loads[j].Source == contact.Source) totalSourceForce += _loads[j].Force;
                        force = EarthSustainedCrush.SleepingPairForce(force, totalSourceForce, contact.Source.mass);
                    }
                    SustainedLoadNewtons += force;
                }
                _loadDwell = EarthSustainedCrush.Step(_loadDwell, SustainedLoadNewtons, targetBody.mass, Time.fixedDeltaTime);
                IsUnderCrushingLoad = _loadDwell >= EarthSustainedCrush.DwellSeconds;
                bool physicalRagdoll = _visibleRagdoll != null && _visibleRagdoll.IsRagdollActive;
                if (physicalRagdoll && Time.time >= _nextPinClearanceProbe &&
                    EarthSustainedCrush.StepPinned(0f, SustainedLoadNewtons, targetBody.mass,
                        true, true, Time.fixedDeltaTime) > 0f)
                {
                    _nextPinClearanceProbe = Time.time + .1f;
                    Vector3 up = _motor != null ? _motor.LocalUp : transform.up;
                    _visibleRagdoll.RefreshRecoveryClearance(up, Vector3.ProjectOnPlane(transform.forward, up));
                }
                _pinnedDwell = EarthSustainedCrush.StepPinned(_pinnedDwell, SustainedLoadNewtons, targetBody.mass,
                    _visibleRagdoll != null && _visibleRagdoll.IsRagdollActive,
                    _visibleRagdoll != null && _visibleRagdoll.RecoveryBlockedByGeometry, Time.fixedDeltaTime);
                float damage = Mathf.Max(EarthSustainedCrush.Damage(_loadDwell,
                    SustainedLoadNewtons, targetBody.mass, Time.fixedDeltaTime),
                    EarthSustainedCrush.PinnedDamage(_pinnedDwell, SustainedLoadNewtons, targetBody.mass, Time.fixedDeltaTime));
                if (damage <= 0f) return;
                var handoff = RagdollHandoff.Uniform(Vector3.zero);
                if (IsUnderCrushingLoad) BeginRecoverableKnockdown(in handoff);
                if (duelController != null && duelController.ApplyDamage(fighterId, damage, in handoff))
                    LastResponse = EarthCharacterImpactResponse.Knockout;
            }
        }

        private void ClearCrushingLoad()
        {
            System.Array.Clear(_loads, 0, _loads.Length);
            _loadDwell = _pinnedDwell = _nextPinClearanceProbe = 0f;
            IsUnderCrushingLoad = false;
            SustainedLoadNewtons = 0f;
        }
    }
}
