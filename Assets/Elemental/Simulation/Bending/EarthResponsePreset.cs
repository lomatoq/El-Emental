using Unity.Mathematics;

namespace Elemental.Simulation.Bending
{
    public static class EarthResponsePreset
    {
        public const float ImpactFlashSeconds = .055f;
        public const float SchoolSeconds = .16f;
        public const float FireContactCooldown = .12f;
        public static bool IsSignal(EarthMaterialFeedbackKind kind) =>
            kind == EarthMaterialFeedbackKind.SchoolSwitch || kind == EarthMaterialFeedbackKind.FireIgnite ||
            kind == EarthMaterialFeedbackKind.FireEnd || kind == EarthMaterialFeedbackKind.FireContact;
        public static int SparkCount(EarthMaterialFeedbackKind kind, uint seed) => kind switch
        {
            EarthMaterialFeedbackKind.SchoolSwitch => 3 + (int)(seed % 4),
            EarthMaterialFeedbackKind.FireIgnite => 4,
            EarthMaterialFeedbackKind.FireContact => 3,
            EarthMaterialFeedbackKind.Impact => 1,
            _ => 0
        };
        public static float SchoolScale(float age, bool reducedMotion)
        {
            if (reducedMotion || !math.isfinite(age) || age < 0 || age >= SchoolSeconds) return 1;
            return 1 + .08f * math.sin(math.PI * age / SchoolSeconds);
        }
    }

    /// <summary>Fixed storage and a hard event budget. Rewind clears time-based history.</summary>
    public sealed class EarthResponseAdmission
    {
        private readonly uint[] sources = new uint[32];
        private readonly uint[] generations = new uint[32];
        private readonly double[] times = new double[32];
        private int used, cursor, frame = -1, admitted;
        private double previousTime = -1;
        public bool Admit(uint source, uint generation, double time, int currentFrame)
        {
            if (!math.isfinite(time)) return false;
            if (time < previousTime) Reset();
            previousTime = time;
            if (currentFrame != frame) { frame = currentFrame; admitted = 0; }
            if (admitted >= 4) return false;
            if (source != 0)
            {
                for (int i = 0; i < used; i++)
                    if (sources[i] == source && generations[i] == generation && time - times[i] < .08) return false;
                sources[cursor] = source; generations[cursor] = generation; times[cursor] = time;
                cursor = (cursor + 1) % sources.Length; used = math.min(used + 1, sources.Length);
            }
            admitted++; return true;
        }
        public void Reset() { used = cursor = admitted = 0; frame = -1; previousTime = -1; }
    }
}
