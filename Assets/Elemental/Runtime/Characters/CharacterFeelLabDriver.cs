using UnityEngine;

namespace Elemental.Runtime.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterFeelLabDriver : MonoBehaviour
    {
        [SerializeField] private ActiveRagdollPuppet puppet;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private Rigidbody[] fallingRocks = System.Array.Empty<Rigidbody>();
        [SerializeField] private Transform planetCenter;
        [SerializeField] private bool autoRun = true;
        [SerializeField, Min(0.25f)] private float impulseInterval = 2f;
        [SerializeField, Min(0f)] private float impulse = 110f;

        private float _elapsed;
        private int _pulseIndex;

        public int PulseCount { get; private set; }

        public void Configure(
            ActiveRagdollPuppet configuredPuppet,
            Rigidbody configuredTargetBody,
            Rigidbody[] configuredRocks,
            Transform configuredPlanetCenter)
        {
            puppet = configuredPuppet;
            targetBody = configuredTargetBody;
            fallingRocks = configuredRocks ?? System.Array.Empty<Rigidbody>();
            planetCenter = configuredPlanetCenter;
        }

        private void FixedUpdate()
        {
            if (!autoRun || puppet == null || targetBody == null)
            {
                return;
            }

            _elapsed += Time.fixedDeltaTime;
            if (_elapsed >= impulseInterval)
            {
                _elapsed = 0f;
                Pulse();
            }

            ResetEscapedRocks();
        }

        public void Pulse()
        {
            Vector3 up = targetBody.position.sqrMagnitude > 0.001f
                ? (targetBody.position - (planetCenter != null ? planetCenter.position : Vector3.zero)).normalized
                : Vector3.up;
            Vector3 tangent = Vector3.Cross(up, (_pulseIndex++ & 1) == 0 ? Vector3.forward : Vector3.right).normalized;
            targetBody.AddForceAtPosition(tangent * impulse, targetBody.worldCenterOfMass + (up * 0.5f), ForceMode.Impulse);
            puppet.InjectImpact(impulse);
            PulseCount++;
        }

        private void ResetEscapedRocks()
        {
            Vector3 center = planetCenter != null ? planetCenter.position : Vector3.zero;
            for (int index = 0; index < fallingRocks.Length; index++)
            {
                Rigidbody rock = fallingRocks[index];
                if (rock == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(rock.position, center);
                if (distance >= 18f && distance <= 48f)
                {
                    continue;
                }

                Vector3 offset = new Vector3((index - (fallingRocks.Length * 0.5f)) * 0.8f, 32f, 2f + (index % 3));
                rock.position = center + offset;
                rock.linearVelocity = Vector3.zero;
                rock.angularVelocity = Vector3.zero;
            }
        }
    }
}
