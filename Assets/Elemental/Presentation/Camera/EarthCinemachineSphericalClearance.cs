using Elemental.Runtime.World;
using Elemental.Simulation.Characters;
using Unity.Cinemachine;
using UnityEngine;

namespace Elemental.Presentation.Camera
{
    /// <summary>
    /// Last-stage render constraint for the spherical world.  It never changes the
    /// motor, armor physics or canonical camera target; only the final Cinemachine
    /// pose is moved out of invalid planet/hero space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EarthCinemachineSphericalClearance : CinemachineExtension
    {
        [SerializeField] private VoxelPlanetBehaviour planet;
        [SerializeField] private Transform hero;
        [SerializeField, Min(0.05f)] private float surfaceClearance = 0.42f;
        [SerializeField, Min(0.25f)] private float minimumHeroDistance = 1.35f;

        public float LastCorrectionMeters { get; private set; }
        public float SurfaceClearance => surfaceClearance;

        public void Configure(VoxelPlanetBehaviour configuredPlanet, Transform configuredHero)
        {
            planet = configuredPlanet;
            hero = configuredHero;
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || planet == null || hero == null)
                return;

            Vector3 desired = state.GetFinalPosition();
            Vector3 up = hero.position - planet.transform.position;
            if (up.sqrMagnitude < 0.01f) up = hero.up;
            up.Normalize();
            Vector3 heroFocus = hero.position + up * 1.05f;
            Vector3 fallbackBack = -Vector3.ProjectOnPlane(hero.forward, up);
            if (fallbackBack.sqrMagnitude < 0.01f) fallbackBack = -hero.forward;
            Vector3 resolved = ToVector3(EarthCameraClearanceSolver.Resolve(
                ToFloat3(desired),
                ToFloat3(planet.transform.position),
                planet.Radius,
                surfaceClearance,
                ToFloat3(heroFocus),
                minimumHeroDistance,
                ToFloat3(fallbackBack)));
            Vector3 correction = resolved - desired;
            LastCorrectionMeters = correction.magnitude;
            state.PositionCorrection += correction;
        }

        private static Unity.Mathematics.float3 ToFloat3(Vector3 value) =>
            new Unity.Mathematics.float3(value.x, value.y, value.z);

        private static Vector3 ToVector3(Unity.Mathematics.float3 value) =>
            new Vector3(value.x, value.y, value.z);
    }
}
