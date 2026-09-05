using Elemental.Simulation.Gravity;
using Unity.Mathematics;
using UnityEngine;
using Elemental.Runtime.World;

namespace Elemental.Runtime.Physics
{
    [DisallowMultipleComponent]
    public sealed class PointPlanetGravitySource : MonoBehaviour
    {
        [SerializeField, Min(1)] private uint fieldId = 1u;
        [SerializeField, Min(0.01f)] private float radius = 1f;
        [SerializeField, Min(0.01f)] private float surfaceAcceleration = 14f;
        [SerializeField, Min(0.01f)] private float innerClampRadius = 2f;
        [SerializeField, Min(0.01f)] private float falloffDistance = 96f;
        [SerializeField, Min(0.01f)] private float falloffExponent = 2f;
        [SerializeField, Min(0.01f)] private float maxAcceleration = 40f;

        public float Radius => radius;
        public float SurfaceAcceleration => surfaceAcceleration;

        public void Configure(
            GravityFieldId id,
            float configuredRadius,
            float configuredSurfaceAcceleration,
            float configuredInnerClampRadius,
            float configuredFalloffDistance,
            float configuredFalloffExponent = 2f,
            float configuredMaxAcceleration = 40f)
        {
            fieldId = id.Value;
            radius = configuredRadius;
            surfaceAcceleration = configuredSurfaceAcceleration;
            innerClampRadius = configuredInnerClampRadius;
            falloffDistance = configuredFalloffDistance;
            falloffExponent = configuredFalloffExponent;
            maxAcceleration = configuredMaxAcceleration;
        }

        public void Configure(GravityFieldId id, PlanetWorldProfile profile)
        {
            if (profile == null) return;
            Configure(
                id,
                profile.Radius,
                profile.SurfaceGravity,
                Mathf.Max(1f, profile.Radius / 12f),
                profile.Radius * 4f,
                2f,
                Mathf.Max(40f, profile.SurfaceGravity * 3f));
        }

        public void Configure(PlanetWorldProfile profile) =>
            Configure(new GravityFieldId(fieldId != 0u ? fieldId : 1u), profile);

        public PointPlanetGravity BuildField()
        {
            Vector3 position = transform.position;
            return new PointPlanetGravity(
                new GravityFieldId(fieldId),
                new float3(position.x, position.y, position.z),
                radius,
                surfaceAcceleration,
                innerClampRadius,
                falloffDistance,
                falloffExponent,
                maxAcceleration);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.18f, 0.78f, 0.74f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
