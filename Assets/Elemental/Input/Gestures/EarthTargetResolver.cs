using Elemental.Runtime.Physics;
using UnityEngine;

namespace Elemental.Input.Gestures
{
    public readonly struct EarthResolvedTarget
    {
        public EarthResolvedTarget(
            EarthSourceKind source,
            Collider collider,
            Rigidbody body,
            IEarthPhysicalTarget physicalTarget,
            IEarthFractureSource fractureSource,
            EarthTargetCapabilities capabilities = EarthTargetCapabilities.None)
        {
            Source = source;
            Collider = collider;
            Body = body;
            PhysicalTarget = physicalTarget;
            FractureSource = fractureSource;
            Capabilities = capabilities != EarthTargetCapabilities.None
                ? capabilities
                : EarthTargetCapabilityResolver.Resolve(source, physicalTarget, fractureSource);
        }

        public EarthSourceKind Source { get; }
        public Collider Collider { get; }
        public Rigidbody Body { get; }
        public IEarthPhysicalTarget PhysicalTarget { get; }
        public IEarthFractureSource FractureSource { get; }
        public EarthTargetCapabilities Capabilities { get; }
        public bool IsValid => Source != EarthSourceKind.Invalid && Collider != null;
    }

    public static class EarthTargetResolver
    {
        public static EarthResolvedTarget Resolve(Collider collider, Collider planetCollider)
        {
            if (collider == null) return default;
            bool isTerrain = collider == planetCollider ||
                             (planetCollider != null && collider.transform.IsChildOf(planetCollider.transform));
            if (isTerrain)
                return new EarthResolvedTarget(
                    EarthSourceKind.Terrain, collider, collider.attachedRigidbody, null, null);

            EarthWallPiece wallPiece = collider.GetComponentInParent<EarthWallPiece>();
            if (wallPiece != null)
                return Broken(collider, wallPiece.Body, wallPiece, wallPiece.Owner);
            EarthPlatformPiece platformPiece = collider.GetComponentInParent<EarthPlatformPiece>();
            if (platformPiece != null)
                return Broken(collider, platformPiece.Body, platformPiece, platformPiece.Owner);
            EarthFragment fragment = collider.GetComponentInParent<EarthFragment>();
            if (fragment != null)
                return new EarthResolvedTarget(
                    EarthSourceKind.Rock, collider, fragment.Body, fragment, null);
            EarthPillarWaveColumn pillar = collider.GetComponentInParent<EarthPillarWaveColumn>();
            if (pillar != null)
                return new EarthResolvedTarget(
                    EarthSourceKind.Rock, collider, pillar.Body, pillar, null);
            EarthArmorPiece armorPiece = collider.GetComponentInParent<EarthArmorPiece>();
            if (armorPiece != null)
                return new EarthResolvedTarget(
                    EarthSourceKind.Rock, collider, armorPiece.Body, armorPiece, null);
            EarthWall wall = collider.GetComponentInParent<EarthWall>();
            if (wall != null)
                return new EarthResolvedTarget(
                    wall.IsCollapsing ? EarthSourceKind.BrokenStructure : EarthSourceKind.IntactStructure,
                    collider,
                    wall.Body,
                    wall,
                    wall);
            EarthPlatform platform = collider.GetComponentInParent<EarthPlatform>();
            if (platform != null)
                return new EarthResolvedTarget(
                    platform.IsFractured ? EarthSourceKind.BrokenStructure : EarthSourceKind.IntactStructure,
                    collider,
                    platform.Body,
                    platform,
                    platform);
            PhysicalImpactTarget physical = collider.GetComponentInParent<PhysicalImpactTarget>();
            if (physical != null)
                return new EarthResolvedTarget(
                    EarthSourceKind.Rock, collider, physical.Body, physical, null);
            Rigidbody genericBody = collider.attachedRigidbody;
            if (genericBody != null && !genericBody.isKinematic)
                return new EarthResolvedTarget(
                    EarthSourceKind.Rock,
                    collider,
                    genericBody,
                    null,
                    null,
                    EarthTargetCapabilities.Push | EarthTargetCapabilities.Damage);
            return default;
        }

        public static IEarthPhysicalTarget ResolvePhysicalTarget(Collider collider) =>
            Resolve(collider, null).PhysicalTarget;

        private static EarthResolvedTarget Broken(
            Collider collider,
            Rigidbody body,
            IEarthPhysicalTarget target,
            IEarthFractureSource source) =>
            new EarthResolvedTarget(
                EarthSourceKind.BrokenStructure, collider, body, target, source);
    }

    public static class EarthTargetCapabilityResolver
    {
        private const EarthTargetCapabilities DynamicEarth =
            EarthTargetCapabilities.Grab | EarthTargetCapabilities.Push |
            EarthTargetCapabilities.Gravity | EarthTargetCapabilities.Damage;

        public static EarthTargetCapabilities Resolve(
            EarthSourceKind source,
            IEarthPhysicalTarget target,
            IEarthFractureSource fractureSource)
        {
            if (source == EarthSourceKind.Terrain)
                return EarthTargetCapabilities.Gravity | EarthTargetCapabilities.Damage |
                       EarthTargetCapabilities.Pluck | EarthTargetCapabilities.Surface |
                       EarthTargetCapabilities.Draw;
            if (fractureSource != null)
            {
                EarthTargetCapabilities structure = EarthTargetCapabilities.Gravity |
                                                    EarthTargetCapabilities.Damage |
                                                    EarthTargetCapabilities.Repair |
                                                    EarthTargetCapabilities.Surface |
                                                    EarthTargetCapabilities.Draw |
                                                    EarthTargetCapabilities.Pluck;
                if (target != null) structure |= DynamicEarth;
                else structure |= EarthTargetCapabilities.Push;
                return structure;
            }
            if (target == null) return EarthTargetCapabilities.None;
            return target.TargetKind switch
            {
                EarthPhysicalTargetKind.Wall => DynamicEarth | EarthTargetCapabilities.Repair |
                                                EarthTargetCapabilities.Surface |
                                                EarthTargetCapabilities.Draw |
                                                EarthTargetCapabilities.Pluck,
                EarthPhysicalTargetKind.Platform => DynamicEarth | EarthTargetCapabilities.Repair |
                                                    EarthTargetCapabilities.Surface |
                                                    EarthTargetCapabilities.Draw |
                                                    EarthTargetCapabilities.Pluck,
                _ => DynamicEarth
            };
        }
    }
}
