using Unity.Mathematics;

namespace Elemental.Simulation.Structures
{
    public readonly struct EarthArenaPropSeatingDecision
    {
        public EarthArenaPropSeatingDecision(bool usesArenaFloor, float shiftAlongUp)
        {
            UsesArenaFloor = usesArenaFloor;
            ShiftAlongUp = math.isfinite(shiftAlongUp) ? shiftAlongUp : 0f;
        }

        public bool UsesArenaFloor { get; }
        public float ShiftAlongUp { get; }
    }

    public readonly struct EarthArenaPropSupportDecision
    {
        public EarthArenaPropSupportDecision(
            bool usesArenaFloor,
            int validSampleCount,
            float shiftAlongUp,
            float minimumGapAfterShift,
            float medianGapAfterShift,
            float maximumGapAfterShift = 0f,
            int contactWitnessCount = 0,
            float contactPatchArea = 0f,
            bool centerSupported = false,
            bool accepted = false)
        {
            UsesArenaFloor = usesArenaFloor;
            ValidSampleCount = math.max(0, validSampleCount);
            ShiftAlongUp = math.isfinite(shiftAlongUp) ? shiftAlongUp : 0f;
            MinimumGapAfterShift = math.isfinite(minimumGapAfterShift)
                ? minimumGapAfterShift
                : 0f;
            MedianGapAfterShift = math.isfinite(medianGapAfterShift)
                ? medianGapAfterShift
                : 0f;
            MaximumGapAfterShift = math.isfinite(maximumGapAfterShift)
                ? maximumGapAfterShift
                : 0f;
            ContactWitnessCount = math.max(0, contactWitnessCount);
            ContactPatchArea = math.isfinite(contactPatchArea)
                ? math.max(0f, contactPatchArea)
                : 0f;
            CenterSupported = centerSupported;
            Accepted = accepted;
        }

        public bool UsesArenaFloor { get; }
        public int ValidSampleCount { get; }
        public float ShiftAlongUp { get; }
        public float MinimumGapAfterShift { get; }
        public float MedianGapAfterShift { get; }
        public float MaximumGapAfterShift { get; }
        public int ContactWitnessCount { get; }
        public float ContactPatchArea { get; }
        public bool CenterSupported { get; }
        public bool Accepted { get; }
    }

    /// <summary>
    /// Pure contract for the editor-side arena adapter. A prop is only moved from
    /// its already-valid spherical seat when the visible arena floor was actually
    /// hit beneath it. This prevents the floor's global AABB maximum from lifting
    /// exterior rocks into empty space.
    /// </summary>
    public static class EarthArenaPropSeatingSolver
    {
        public const int MaximumSupportSamples = 8;
        public const float MinimumAcceptedGap = -0.010f;
        public const float MaximumAcceptedGap = 0.015f;

        public static EarthArenaPropSeatingDecision Resolve(
            float currentMinimumProjection,
            bool hasFloorHit,
            float floorSurfaceProjection,
            float contactInset)
        {
            if (!hasFloorHit ||
                !math.isfinite(currentMinimumProjection) ||
                !math.isfinite(floorSurfaceProjection))
                return new EarthArenaPropSeatingDecision(false, 0f);

            float inset = math.clamp(
                math.isfinite(contactInset) ? contactInset : 0f,
                0f,
                0.08f);
            return new EarthArenaPropSeatingDecision(
                true,
                floorSurfaceProjection - inset - currentMinimumProjection);
        }

        /// <summary>
        /// Resolves a visible contact patch instead of an AABB minimum. Arrays are
        /// bounded editor-authoring buffers (maximum eight samples in the adapter),
        /// so this method remains deterministic and independent of Unity objects.
        /// </summary>
        public static EarthArenaPropSupportDecision ResolveSupportPatch(
            float[] propPointProjections,
            bool[] floorHits,
            float[] floorSurfaceProjections,
            int sampleCount,
            float contactInset)
        {
            return ResolveSupportPatch(
                propPointProjections,
                floorHits,
                floorSurfaceProjections,
                null,
                default,
                sampleCount,
                contactInset,
                false);
        }

        /// <summary>
        /// Resolves the vertical seat and validates that the resulting near-contact
        /// witnesses form a real support polygon around the projected centre of
        /// mass. Tangent points are the support-hit positions in a common 2D frame.
        /// </summary>
        public static EarthArenaPropSupportDecision ResolveSupportPatch(
            float[] propPointProjections,
            bool[] floorHits,
            float[] floorSurfaceProjections,
            float2[] tangentSupportPoints,
            float2 projectedCenterOfMass,
            int sampleCount,
            float contactInset,
            bool requireSupportedCenter)
        {
            if (propPointProjections == null || floorHits == null ||
                floorSurfaceProjections == null)
                return default;
            int count = math.min(MaximumSupportSamples, math.min(
                math.max(0, sampleCount),
                math.min(propPointProjections.Length,
                    math.min(floorHits.Length, floorSurfaceProjections.Length))));
            if (count <= 0) return default;

            float inset = math.clamp(
                math.isfinite(contactInset) ? contactInset : 0f,
                0f,
                0.08f);
            float[] corrections = new float[count];
            int valid = 0;
            for (int index = 0; index < count; index++)
            {
                if (!floorHits[index] ||
                    !math.isfinite(propPointProjections[index]) ||
                    !math.isfinite(floorSurfaceProjections[index])) continue;
                float correction = floorSurfaceProjections[index] - inset -
                                   propPointProjections[index];
                int insert = valid;
                while (insert > 0 && corrections[insert - 1] > correction)
                {
                    corrections[insert] = corrections[insert - 1];
                    insert--;
                }
                corrections[insert] = correction;
                valid++;
            }
            if (valid == 0) return default;

            float medianShift = valid % 2 == 1
                ? corrections[valid / 2]
                : (corrections[valid / 2 - 1] + corrections[valid / 2]) * 0.5f;
            // The median rejects one bad ray, but it must never authorize a
            // visible vertex to penetrate farther than the controlled inset.
            // Clamp upward to the strictest valid contact witness.
            float shift = medianShift;
            // Median seating rejects an isolated bad ray. The final lower-bound
            // guard is applied only after the median solve so no accepted point can
            // penetrate deeper than the controlled one-centimetre inset.
            float minimumBeforeGuard = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                if (!floorHits[index] ||
                    !math.isfinite(propPointProjections[index]) ||
                    !math.isfinite(floorSurfaceProjections[index])) continue;
                minimumBeforeGuard = math.min(
                    minimumBeforeGuard,
                    propPointProjections[index] + shift - floorSurfaceProjections[index]);
            }
            if (math.isfinite(minimumBeforeGuard) && minimumBeforeGuard < MinimumAcceptedGap)
                shift += MinimumAcceptedGap - minimumBeforeGuard;
            float minimumGap = float.PositiveInfinity;
            float maximumGap = float.NegativeInfinity;
            float[] gaps = new float[valid];
            int gapIndex = 0;
            for (int index = 0; index < count; index++)
            {
                if (!floorHits[index] ||
                    !math.isfinite(propPointProjections[index]) ||
                    !math.isfinite(floorSurfaceProjections[index])) continue;
                float gap = propPointProjections[index] + shift -
                            floorSurfaceProjections[index];
                minimumGap = math.min(minimumGap, gap);
                maximumGap = math.max(maximumGap, gap);
                int insert = gapIndex;
                while (insert > 0 && gaps[insert - 1] > gap)
                {
                    gaps[insert] = gaps[insert - 1];
                    insert--;
                }
                gaps[insert] = gap;
                gapIndex++;
            }
            float medianGap = valid % 2 == 1
                ? gaps[valid / 2]
                : (gaps[valid / 2 - 1] + gaps[valid / 2]) * 0.5f;
            int contactCount = 0;
            float2[] contact = new float2[MaximumSupportSamples];
            bool hasTangentPoints = tangentSupportPoints != null &&
                                    tangentSupportPoints.Length >= count;
            for (int index = 0; index < count; index++)
            {
                if (!floorHits[index] ||
                    !math.isfinite(propPointProjections[index]) ||
                    !math.isfinite(floorSurfaceProjections[index])) continue;
                // The contact patch is the footprint of valid support rays, not
                // only the single closest vertex. On cratered floors and the
                // planet sphere a rigid rock naturally has one closest witness;
                // the surrounding valid rays still define whether its centre of
                // mass is supported. The two-sided gap gate remains on the actual
                // closest visible witness above.
                float2 point = hasTangentPoints ? tangentSupportPoints[index] : default;
                if (hasTangentPoints && !math.all(math.isfinite(point))) continue;
                bool duplicate = false;
                for (int witness = 0; witness < contactCount; witness++)
                {
                    if (math.distancesq(contact[witness], point) > 0.000025f) continue;
                    duplicate = true;
                    break;
                }
                if (!duplicate) contact[contactCount++] = point;
            }

            bool supported = false;
            float patchArea = hasTangentPoints
                ? ConvexHullAreaAndSupport(contact, contactCount, projectedCenterOfMass, out supported)
                : 0f;
            bool centerSupported = hasTangentPoints ? supported : !requireSupportedCenter;
            bool gapAccepted = minimumGap >= MinimumAcceptedGap - 0.0001f &&
                               minimumGap <= MaximumAcceptedGap + 0.0001f;
            bool patchAccepted = !requireSupportedCenter ||
                                 (contactCount >= 3 && patchArea >= 0.000025f && centerSupported);
            return new EarthArenaPropSupportDecision(
                true,
                valid,
                shift,
                minimumGap,
                medianGap,
                maximumGap,
                contactCount,
                patchArea,
                centerSupported,
                gapAccepted && patchAccepted);
        }

        private static float ConvexHullAreaAndSupport(
            float2[] points,
            int count,
            float2 center,
            out bool centerSupported)
        {
            centerSupported = false;
            if (points == null || count < 3 || !math.all(math.isfinite(center))) return 0f;
            float2[] sorted = new float2[count];
            for (int index = 0; index < count; index++) sorted[index] = points[index];
            for (int index = 1; index < count; index++)
            {
                float2 value = sorted[index];
                int insert = index;
                while (insert > 0 && Compare(value, sorted[insert - 1]) < 0)
                {
                    sorted[insert] = sorted[insert - 1];
                    insert--;
                }
                sorted[insert] = value;
            }

            float2[] hull = new float2[count * 2];
            int hullCount = 0;
            for (int index = 0; index < count; index++)
            {
                while (hullCount >= 2 && Cross(
                           hull[hullCount - 1] - hull[hullCount - 2],
                           sorted[index] - hull[hullCount - 1]) <= 0f)
                    hullCount--;
                hull[hullCount++] = sorted[index];
            }
            int lowerCount = hullCount;
            for (int index = count - 2; index >= 0; index--)
            {
                while (hullCount > lowerCount && Cross(
                           hull[hullCount - 1] - hull[hullCount - 2],
                           sorted[index] - hull[hullCount - 1]) <= 0f)
                    hullCount--;
                hull[hullCount++] = sorted[index];
            }
            if (hullCount > 1) hullCount--;
            if (hullCount < 3) return 0f;

            float signedTwiceArea = 0f;
            centerSupported = true;
            for (int index = 0; index < hullCount; index++)
            {
                float2 a = hull[index];
                float2 b = hull[(index + 1) % hullCount];
                signedTwiceArea += Cross(a, b);
                if (Cross(b - a, center - a) < -0.0025f)
                    centerSupported = false;
            }
            return math.abs(signedTwiceArea) * 0.5f;
        }

        private static int Compare(float2 a, float2 b)
        {
            if (a.x < b.x) return -1;
            if (a.x > b.x) return 1;
            return a.y.CompareTo(b.y);
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;
    }
}
