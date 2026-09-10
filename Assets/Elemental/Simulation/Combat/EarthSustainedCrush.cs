using Unity.Mathematics;

namespace Elemental.Simulation.Combat
{
    public readonly struct EarthSettledLoad
    {
        public EarthSettledLoad(int samples, float mean) { Samples = samples; Mean = mean; }
        public int Samples { get; }
        public float Mean { get; }
        public float RetainedForce => Samples >= 5 ? Mean : 0f;
    }

    // Force is measured support load, not nearby rock mass or impact damage.
    public static class EarthSustainedCrush
    {
        public const float DwellSeconds = .2f;
        // Rough rock can carry vertical weight on an oblique contact. The
        // measured impulse already supplies its vertical component; a walkable
        // floor normal threshold must not discard that transmitted weight.
        public static float OverheadContactForce(float3 impulse, float3 sourceNormal, float3 up,
            float sourceHeightAboveContact, float deltaTime)
        {
            if (!math.all(math.isfinite(impulse)) || !math.all(math.isfinite(sourceNormal)) ||
                !math.all(math.isfinite(up)) || !float.IsFinite(sourceHeightAboveContact) ||
                !float.IsFinite(deltaTime) || sourceHeightAboveContact <= 0f || deltaTime <= 0f) return 0f;
            up = math.normalizesafe(up);
            if (math.dot(math.normalizesafe(sourceNormal), up) <= .05f) return 0f;
            return math.abs(math.dot(impulse, up)) / deltaTime;
        }

        public static float Step(float elapsed, float loadNewtons, float targetMass, float deltaTime)
        {
            if (!float.IsFinite(loadNewtons) || !float.IsFinite(targetMass) ||
                !float.IsFinite(deltaTime) || !float.IsFinite(elapsed) || targetMass <= 0f || deltaTime <= 0f ||
                loadNewtons < math.max(100f, targetMass * 2f) * 14f) return 0f;
            return math.min(DwellSeconds, math.max(0f, elapsed) + deltaTime);
        }

        public const float PinnedDwellSeconds = 1.25f;

        public static float StepPinned(float elapsed, float loadNewtons, float targetMass,
            bool physicalRagdoll, bool recoveryBlockedByGeometry, float deltaTime)
        {
            if (!physicalRagdoll || !recoveryBlockedByGeometry || !float.IsFinite(elapsed) ||
                !float.IsFinite(loadNewtons) || !float.IsFinite(targetMass) || !float.IsFinite(deltaTime) ||
                targetMass <= 0f || deltaTime <= 0f || loadNewtons < math.max(25f, targetMass * .5f) * 14f) return 0f;
            return math.min(PinnedDwellSeconds, math.max(0f, elapsed) + deltaTime);
        }

        // A constrained ragdoll/stone pair unloads briefly as PhysX redistributes
        // weight between bones. Preserve qualification, not damage, across that gap.
        public const float PinnedUnloadingGraceSeconds = .30f;
        public static float StepPinnedWithBriefUnloading(float elapsed, ref float unloadedSeconds,
            float loadNewtons, float targetMass, bool physicalRagdoll, bool recoveryBlockedByGeometry, float deltaTime)
        {
            if (!physicalRagdoll || !recoveryBlockedByGeometry || !float.IsFinite(elapsed) ||
                !float.IsFinite(unloadedSeconds) || !float.IsFinite(loadNewtons) || !float.IsFinite(targetMass) ||
                !float.IsFinite(deltaTime) || targetMass<=0f || deltaTime<=0f || loadNewtons<0f)
            { unloadedSeconds=0f;return 0f; }
            float qualified=StepPinned(elapsed,loadNewtons,targetMass,true,true,deltaTime);
            if (qualified>0f) { unloadedSeconds=0f;return qualified; }
            unloadedSeconds=math.min(PinnedUnloadingGraceSeconds,math.max(0f,unloadedSeconds)+deltaTime);
            return unloadedSeconds<PinnedUnloadingGraceSeconds?math.clamp(elapsed,0f,PinnedDwellSeconds):0f;
        }

        public static float PinnedDamage(float elapsed, float loadNewtons, float targetMass, float deltaTime)
        {
            if (!float.IsFinite(elapsed) || elapsed < PinnedDwellSeconds || !float.IsFinite(loadNewtons) ||
                !float.IsFinite(targetMass) || !float.IsFinite(deltaTime) || targetMass <= 0f || deltaTime <= 0f ||
                loadNewtons < math.max(25f, targetMass * .5f) * 14f) return 0f;
            return math.clamp(loadNewtons / (targetMass * 14f) * 12f, 8f, 18f) * deltaTime;
        }

        public static EarthSettledLoad SampleSettledLoad(in EarthSettledLoad previous,
            float force, float relativeSpeed, bool consecutive)
        {
            if (!float.IsFinite(force) || !float.IsFinite(relativeSpeed) || force <= 0f || relativeSpeed > .75f || relativeSpeed < 0f)
                return default;
            if (!consecutive || previous.Samples == 0 || force < previous.Mean * .6f || force > previous.Mean * 1.4f)
                return new EarthSettledLoad(1, force);
            int count = math.min(8, previous.Samples + 1);
            return new EarthSettledLoad(count, previous.Mean + (force - previous.Mean) / count);
        }

        public static float SleepingPairForce(float pairForce, float totalSourceForce, float sourceMass)
        {
            if (!float.IsFinite(pairForce) || !float.IsFinite(totalSourceForce) || !float.IsFinite(sourceMass) ||
                pairForce <= 0f || totalSourceForce <= 0f || sourceMass <= 0f) return 0f;
            return pairForce * math.min(1f, sourceMass * 14f / totalSourceForce);
        }

        public static float Damage(float elapsed, float loadNewtons, float targetMass, float deltaTime)
        {
            if (!float.IsFinite(elapsed) || elapsed < DwellSeconds || !float.IsFinite(loadNewtons) ||
                !float.IsFinite(targetMass) || targetMass <= 0f || !float.IsFinite(deltaTime) || deltaTime <= 0f ||
                loadNewtons < math.max(100f, targetMass * 2f) * 14f) return 0f;
            return math.clamp(loadNewtons / (math.max(100f, targetMass * 2f) * 14f), 1f, 3f) * 15f * deltaTime;
        }
    }
}
