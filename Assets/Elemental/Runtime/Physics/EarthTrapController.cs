using System;
using Elemental.Simulation.Bending;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    /// <summary>
    /// Small sandbox trap/cuff. It constrains a dynamic target with forces rather
    /// than parenting or teleporting it, and always has a bounded release path.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EarthTrapController : MonoBehaviour
    {
        [SerializeField] private bool armedOnStart = true;
        [SerializeField, Min(0.1f)] private float holdSeconds = 2.4f;
        [SerializeField, Min(1f)] private float breakImpulse = 380f;
        [SerializeField, Min(1f)] private float stiffness = 46f;
        [SerializeField, Min(0f)] private float damping = 9.5f;

        private Rigidbody _captured;
        private float _capturedAt;

        public EarthTrapState State { get; private set; }
        public Rigidbody CapturedBody => _captured;
        public event Action<EarthTrapState> StateChanged;

        public void Configure(float configuredHoldSeconds, float configuredBreakImpulse, bool arm)
        {
            holdSeconds = Mathf.Max(0.1f, configuredHoldSeconds);
            breakImpulse = Mathf.Max(1f, configuredBreakImpulse);
            if (arm) Arm();
        }

        public void Arm()
        {
            _captured = null;
            SetState(EarthTrapState.Armed);
        }

        public void Release()
        {
            _captured = null;
            SetState(EarthTrapState.Spent);
        }

        private void Awake()
        {
            Collider volume = GetComponent<Collider>();
            volume.isTrigger = true;
            State = armedOnStart ? EarthTrapState.Armed : EarthTrapState.Dormant;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (State != EarthTrapState.Armed || other == null) return;
            Rigidbody candidate = other.attachedRigidbody;
            if (candidate == null || candidate.isKinematic) return;
            _captured = candidate;
            _capturedAt = Time.time;
            SetState(EarthTrapState.Captured);
        }

        private void FixedUpdate()
        {
            if (State != EarthTrapState.Captured || _captured == null)
            {
                if (State == EarthTrapState.Captured) Release();
                return;
            }
            float elapsed = Mathf.Max(0f, Time.time - _capturedAt);
            float escapeImpulse = _captured.linearVelocity.magnitude * _captured.mass;
            EarthTrapSample sample = EarthTrapSolver.Step(
                State, elapsed, holdSeconds, escapeImpulse, breakImpulse);
            if (sample.Release)
            {
                Release();
                return;
            }

            Vector3 localUp = transform.position.sqrMagnitude > 0.01f
                ? transform.position.normalized
                : transform.up;
            Vector3 anchor = transform.position + localUp * 0.28f;
            Vector3 error = anchor - _captured.worldCenterOfMass;
            Vector3 tangentVelocity = Vector3.ProjectOnPlane(_captured.linearVelocity, localUp);
            Vector3 force = error * stiffness * sample.Strength01 - tangentVelocity * damping;
            _captured.AddForce(force, ForceMode.Acceleration);
        }

        private void SetState(EarthTrapState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
