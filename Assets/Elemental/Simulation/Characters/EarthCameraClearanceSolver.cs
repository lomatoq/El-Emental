using Unity.Mathematics;

namespace Elemental.Simulation.Characters
{
    /// <summary>
    /// Pure final-camera constraint for a spherical world.  A long third-person
    /// camera arm is a chord, so it can dip through a small planet even when its
    /// target and end points appear to be above the ground.  This solver keeps the
    /// rendered camera outside both the planet shell and the hero's personal space.
    /// </summary>
    public static class EarthCameraClearanceSolver
    {
        public static float3 Resolve(
            float3 desiredPosition,
            float3 planetCenter,
            float planetRadius,
            float surfaceClearance,
            float3 heroFocus,
            float minimumHeroDistance,
            float3 fallbackBack)
        {
            if (!math.all(math.isfinite(desiredPosition)) ||
                !math.all(math.isfinite(planetCenter)) ||
                !math.all(math.isfinite(heroFocus)))
                return desiredPosition;

            float3 resolved = desiredPosition;
            float minimumRadius = math.max(0f, planetRadius) + math.max(0f, surfaceClearance);
            if (minimumRadius > 0f)
            {
                float3 radial = resolved - planetCenter;
                float radialLength = math.length(radial);
                if (radialLength < minimumRadius)
                {
                    float3 fallbackUp = math.normalizesafe(heroFocus - planetCenter, new float3(0f, 1f, 0f));
                    resolved = planetCenter + math.normalizesafe(radial, fallbackUp) * minimumRadius;
                }
            }

            float personalDistance = math.max(0f, minimumHeroDistance);
            float3 fromHero = resolved - heroFocus;
            float heroDistance = math.length(fromHero);
            if (heroDistance < personalDistance)
            {
                float3 back = math.normalizesafe(fromHero,
                    math.normalizesafe(fallbackBack, new float3(0f, 0f, -1f)));
                resolved = heroFocus + back * personalDistance;

                // Personal-space correction can itself re-enter the spherical shell.
                if (minimumRadius > 0f)
                {
                    float3 radial = resolved - planetCenter;
                    float radialLength = math.length(radial);
                    if (radialLength < minimumRadius)
                    {
                        float3 fallbackUp = math.normalizesafe(heroFocus - planetCenter, new float3(0f, 1f, 0f));
                        resolved = planetCenter + math.normalizesafe(radial, fallbackUp) * minimumRadius;
                    }
                }
            }

            return resolved;
        }
    }
}
