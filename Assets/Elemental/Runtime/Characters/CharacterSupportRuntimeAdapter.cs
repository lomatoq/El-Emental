using Elemental.Runtime.Physics;
using Elemental.Simulation.Characters;
using UnityEngine;

namespace Elemental.Runtime.Characters
{
    /// <summary>
    /// Converts Unity collider ownership into the pure support-selection contract.
    /// Dynamic matter is classified before parent surfaces so a released child
    /// cannot inherit walkable authority from its former structure.
    /// </summary>
    public static class CharacterSupportRuntimeAdapter
    {
        public static CharacterSupportCandidate Classify(
            Collider collider,
            float distance,
            float upDot)
        {
            if (collider == null)
                return default;

            IEarthPhysicalTarget physicalTarget = collider.GetComponentInParent(
                typeof(IEarthPhysicalTarget)) as IEarthPhysicalTarget;
            if (physicalTarget != null &&
                physicalTarget.TargetKind != EarthPhysicalTargetKind.Platform &&
                physicalTarget.TargetKind != EarthPhysicalTargetKind.Wall)
            {
                EarthPhysicalTargetHandle handle = physicalTarget.TargetHandle;
                CharacterSupportKind rejectedKind =
                    physicalTarget.TargetKind == EarthPhysicalTargetKind.PlatformPiece ||
                    physicalTarget.TargetKind == EarthPhysicalTargetKind.WallPiece
                        ? CharacterSupportKind.ReleasedFracture
                        : CharacterSupportKind.DynamicDebris;
                return Candidate(
                    handle.IsValid ? handle.StableId : StableColliderId(collider),
                    handle.IsValid ? handle.Generation : 1u,
                    rejectedKind,
                    distance,
                    upDot,
                    false);
            }

            EarthArenaSurfaceProvider arena =
                collider.GetComponentInParent<EarthArenaSurfaceProvider>();
            if (arena != null && arena.TryGetCharacterSupport(
                    collider,
                    out uint arenaId,
                    out uint arenaGeneration))
                return Candidate(
                    arenaId,
                    arenaGeneration,
                    CharacterSupportKind.ArenaWalkableProxy,
                    distance,
                    upDot,
                    true);

            VoxelPlanetEarthSurfaceProvider planet =
                collider.GetComponentInParent<VoxelPlanetEarthSurfaceProvider>();
            if (planet != null && planet.TryGetCharacterSupport(
                    collider,
                    out uint planetId,
                    out uint planetGeneration))
                return Candidate(
                    planetId,
                    planetGeneration,
                    CharacterSupportKind.PlanetGround,
                    distance,
                    upDot,
                    true);

            IMovingSurface movingSurface = collider.GetComponentInParent(
                typeof(IMovingSurface)) as IMovingSurface;
            if (movingSurface != null)
            {
                var frame = movingSurface.SupportFrame;
                uint generation = frame.IsValid ? frame.Generation : 1u;
                return Candidate(
                    movingSurface.SurfaceId,
                    generation,
                    CharacterSupportKind.MovingAbilitySurface,
                    distance,
                    upDot,
                    movingSurface.SurfaceId != 0u);
            }

            EarthWall wall = collider.GetComponentInParent<EarthWall>();
            if (wall != null && wall.IsSurfaceAvailable && wall.SurfaceCollider == collider)
                return Candidate(
                    wall.WallId,
                    wall.Generation,
                    CharacterSupportKind.MovingAbilitySurface,
                    distance,
                    upDot,
                    true);

            Rigidbody body = collider.attachedRigidbody;
            if (body != null && !body.isKinematic)
                return Candidate(
                    StableColliderId(collider),
                    1u,
                    CharacterSupportKind.DynamicDebris,
                    distance,
                    upDot,
                    false);

            // Legacy static proxies predate the explicit layer taxonomy. Keep
            // them walkable during migration, but give them a stable per-session
            // handle so selection remains deterministic within the scene.
            return Candidate(
                StableColliderId(collider),
                1u,
                CharacterSupportKind.PlanetGround,
                distance,
                upDot,
                true);
        }

        private static CharacterSupportCandidate Candidate(
            uint surfaceId,
            uint generation,
            CharacterSupportKind kind,
            float distance,
            float upDot,
            bool walkable) =>
            new CharacterSupportCandidate(
                surfaceId != 0u ? surfaceId : 1u,
                generation != 0u ? generation : 1u,
                kind,
                distance,
                upDot,
                true,
                walkable);

        private static uint StableColliderId(Collider collider)
        {
            uint value = unchecked((uint)collider.GetEntityId().GetHashCode());
            return value != 0u ? value : 1u;
        }
    }
}
