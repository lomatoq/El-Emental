using UnityEngine;

namespace Elemental.Runtime.Missions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MissionTerrainLever : MonoBehaviour
    {
        [SerializeField] private MissionDirectorBehaviour director;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private bool opensRoute;
        [SerializeField] private bool damagesStructure;
        [SerializeField, Min(0.1f)] private float triggerImpulse = 5f;
        private bool _triggered;

        public void Configure(MissionDirectorBehaviour configuredDirector, Rigidbody configuredBody, bool configuredOpensRoute, bool configuredDamagesStructure, float configuredTriggerImpulse)
        {
            director = configuredDirector; targetBody = configuredBody; opensRoute = configuredOpensRoute;
            damagesStructure = configuredDamagesStructure; triggerImpulse = Mathf.Max(0.1f, configuredTriggerImpulse);
        }

        private void Awake()
        {
            if (targetBody == null) targetBody = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_triggered || collision.impulse.magnitude < triggerImpulse) return;
            _triggered = true;
            director?.ApplyTerrainChange(opensRoute, damagesStructure);
        }
    }
}
