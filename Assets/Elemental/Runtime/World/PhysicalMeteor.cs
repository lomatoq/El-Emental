using UnityEngine;

namespace Elemental.Runtime.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicalMeteor : MonoBehaviour
    {
        private MeteorShowerBehaviour _owner;

        public Rigidbody Body { get; private set; }
        public uint MeteorId { get; private set; }
        public float Radius { get; private set; }

        public void Configure(MeteorShowerBehaviour owner, Rigidbody body)
        {
            _owner = owner;
            Body = body;
        }

        public void Activate(uint id, Vector3 position, float radius, float mass, Vector3 velocity)
        {
            MeteorId = id;
            Radius = Mathf.Max(0.05f, radius);
            transform.position = position;
            transform.localScale = Vector3.one * Radius * 2f;
            gameObject.SetActive(true);
            Body.mass = Mathf.Max(0.1f, mass);
            Body.linearVelocity = velocity;
            Body.angularVelocity = DeterministicSpin(id) * 4f;
            Body.WakeUp();
        }

        public void Deactivate()
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            gameObject.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision) => _owner?.ResolveImpact(this, collision);

        private static Vector3 DeterministicSpin(uint id)
        {
            uint x = id * 0x9E3779B9u + 0x85EBCA6Bu;
            x ^= x >> 16;
            float a = (x & 1023u) / 1023f * Mathf.PI * 2f;
            float y = ((x >> 10) & 1023u) / 511.5f - 1f;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
        }
    }
}
