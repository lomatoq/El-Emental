using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Presentation.VFX
{
    [DefaultExecutionOrder(600)]
    public sealed class EarthCharacterGroundFeedback : MonoBehaviour
    {
        [SerializeField] private PlanetMotor motor;
        [SerializeField] private EarthPillarMobility pillar;
        [SerializeField] private EarthMaterialFeedbackHub hub;
        [SerializeField] private Animator animator;
        private Transform left, right;
        private bool initialized, supported, leftContact, rightContact;
        private float impactSpeed, nextRoll, nextLeft, nextRight;
        private readonly RaycastHit[] supportHits = new RaycastHit[16];
        public void Configure(PlanetMotor movement, EarthPillarMobility launch, EarthMaterialFeedbackHub events, Animator rig)
        {
            if (pillar != null) pillar.PillarRaised -= Raised;
            motor = movement; pillar = launch; hub = events; animator = rig;
            ResolveBones();
            ResetContacts();
            if (isActiveAndEnabled && pillar != null) pillar.PillarRaised += Raised;
        }
        private void OnEnable()
        {
            ResolveBones();
            ResetContacts();
            if (pillar != null) { pillar.PillarRaised -= Raised; pillar.PillarRaised += Raised; }
        }
        private void OnDisable() { if (pillar != null) pillar.PillarRaised -= Raised; }
        private void Start() => ResolveBones();
        private void ResolveBones()
        {
            left = right = null;
            if (animator != null && animator.avatar != null && animator.avatar.isValid && animator.isHuman)
            { left = animator.GetBoneTransform(HumanBodyBones.LeftFoot); right = animator.GetBoneTransform(HumanBodyBones.RightFoot); }
        }
        private void ResetContacts()
        {
            initialized = supported = leftContact = rightContact = false;
            impactSpeed = nextRoll = nextLeft = nextRight = 0f;
        }
        private void Raised(EarthPillarLaunchEvent value) => hub?.Emit(EarthMaterialFeedbackKind.Emerge,
            value.SurfaceBase, value.LocalUp, 1f + value.Charge01, value.Radius, value.Tick, dustCount: 48, chipCount: 12);
        private void LateUpdate()
        {
            if (hub == null || motor == null || motor.Body == null) return;
            bool ground = motor.HasStableSupport;
            Vector3 up = motor.LocalUp;
            Vector3 velocity = motor.Body.linearVelocity;
            float speed = Vector3.ProjectOnPlane(velocity, up).magnitude;
            RaycastHit rootContact = default;
            bool hasContact = ground && (!supported || motor.LandingRollActive) &&
                TrySupport(motor.Body.worldCenterOfMass + up * .2f, up, 2f, out rootContact);
            if (!initialized) { initialized = true; supported = ground; }
            if (!ground) impactSpeed = Mathf.Max(impactSpeed, -Vector3.Dot(velocity, up));
            if (ground && !supported && impactSpeed > 2f && hasContact)
            { hub.Emit(EarthMaterialFeedbackKind.Land, rootContact.point, rootContact.normal, Mathf.Clamp(impactSpeed / 7f, .3f, 2.5f), .5f, dustCount: 32, chipCount: 8); impactSpeed = 0f; }
            if (ground && !supported) impactSpeed = 0f;
            supported = ground;
            if (!ground) { leftContact = rightContact = false; return; }
            if (motor.LandingRollActive)
            {
                if (hasContact && Time.time >= nextRoll) { nextRoll = Time.time + .12f; hub.Emit(EarthMaterialFeedbackKind.Roll, rootContact.point, rootContact.normal, .75f, .25f); }
                return;
            }
            if (speed < .7f) return;
            Step(left, ref leftContact, ref nextLeft, up, speed);
            Step(right, ref rightContact, ref nextRight, up, speed);
        }
        private void Step(Transform foot, ref bool contact, ref float next, Vector3 up, float speed)
        {
            if (foot == null) return;
            bool hit = TrySupport(foot.position + up * .12f, up, .26f, out RaycastHit support);
            if (hit && !contact && Time.time >= next)
            { next = Time.time + .16f; hub.Emit(EarthMaterialFeedbackKind.Footstep, support.point, support.normal, Mathf.Clamp(speed / 5f, .4f, 1.4f), .15f); }
            contact = hit;
        }
        private bool TrySupport(Vector3 origin, Vector3 up, float distance, out RaycastHit support)
        {
            support = default;
            int count = UnityEngine.Physics.RaycastNonAlloc(origin, -up, supportHits,
                distance, motor.GroundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = supportHits[i];
                if (hit.collider == null || hit.collider.attachedRigidbody == motor.Body ||
                    hit.collider.transform.IsChildOf(motor.transform) || hit.distance >= nearest ||
                    Vector3.Dot(hit.normal, up) < .25f) continue;
                support = hit; nearest = hit.distance; found = true;
            }
            return found;
        }
    }
}
