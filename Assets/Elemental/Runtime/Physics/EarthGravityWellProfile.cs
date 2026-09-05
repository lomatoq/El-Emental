using UnityEngine;
using Elemental.Simulation.Bending;

namespace Elemental.Runtime.Physics
{
    [CreateAssetMenu(menuName = "Elemental/Magic/Earth Gravity Well Profile", fileName = "EarthGravityWellProfile")]
    public sealed class EarthGravityWellProfile : ScriptableObject
    {
        [Header("Field")]
        [SerializeField, Min(0.5f)] private float radius = 7.5f;
        [SerializeField, Min(0f)] private float pullAcceleration = 38f;
        [SerializeField, Min(0f)] private float orbitAcceleration = 5.5f;
        [SerializeField, Min(0f)] private float velocityDamping = 1.8f;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 16f;
        [SerializeField, Min(0.1f)] private float coreRadius = 0.9f;
        [SerializeField, Min(0f)] private float focusLift = 0.75f;
        [Header("Latched fracture cluster")]
        [SerializeField, Range(1, 48)] private int maximumCapturedTargets = 48;
        [SerializeField, Min(0f)] private float clusterStiffness = 16f;
        [SerializeField, Min(0f)] private float clusterDamping = 5.5f;
        [SerializeField, Min(0f)] private float clusterOrbitRadius = 1.35f;
        [SerializeField, Min(0f)] private float clusterAngularDamping = 6.5f;
        [SerializeField, Min(0.1f)] private float clusterMaximumAcceleration = 62f;
        [Header("Cluster launch")]
        [SerializeField, Min(0.1f)] private float fullChargeSeconds = 1.05f;
        [SerializeField, Min(0.01f)] private float directTapSeconds = 0.22f;
        [SerializeField, Min(1f)] private float directLaunchSpeed = 15f;
        [SerializeField, Min(1f)] private float minimumBlastSpeed = 19f;
        [SerializeField, Min(1f)] private float maximumBlastSpeed = 31f;
        [Header("Structure stress")]
        [SerializeField, Min(0.05f)] private float fractureDelaySeconds = 0.68f;
        [SerializeField, Min(0f)] private float fractureImpulse = 1450f;
        [SerializeField, Min(0f)] private float sustainedDamageImpulsePerSecond = 680f;

        public float Radius => radius;
        public float PullAcceleration => pullAcceleration;
        public float OrbitAcceleration => orbitAcceleration;
        public float VelocityDamping => velocityDamping;
        public float MaximumSpeed => maximumSpeed;
        public float CoreRadius => Mathf.Min(coreRadius, radius * 0.8f);
        public float FocusLift => focusLift;
        public float FullChargeSeconds => fullChargeSeconds;
        public float DirectTapSeconds => directTapSeconds;
        public EarthGravityClusterThrowTuning ThrowTuning => new EarthGravityClusterThrowTuning(
            directLaunchSpeed, minimumBlastSpeed, maximumBlastSpeed, 0.34f, 7.5f, 65f);
        // Assets authored before the latched-cluster upgrade deserialize new fields as zero.
        // Keep those profiles functional without requiring a destructive re-create/migration.
        public int MaximumCapturedTargets => maximumCapturedTargets > 0
            ? Mathf.Clamp(maximumCapturedTargets, 1, 48)
            : 48;
        public float ClusterStiffness => clusterStiffness > 0f ? clusterStiffness : 16f;
        public float ClusterDamping => clusterDamping > 0f ? clusterDamping : 5.5f;
        public float ClusterOrbitRadius => clusterOrbitRadius > 0f ? clusterOrbitRadius : 1.35f;
        public float ClusterAngularDamping => clusterAngularDamping > 0f ? clusterAngularDamping : 6.5f;
        public float ClusterMaximumAcceleration => clusterMaximumAcceleration > 0f
            ? clusterMaximumAcceleration
            : 62f;
        public float FractureDelaySeconds => fractureDelaySeconds;
        public float FractureImpulse => fractureImpulse;
        public float SustainedDamageImpulsePerSecond => sustainedDamageImpulsePerSecond;
    }
}
