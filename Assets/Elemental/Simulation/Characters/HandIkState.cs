namespace Elemental.Simulation.Characters
{
    public enum HandIkState : byte
    {
        Inactive = 0,
        BlendingIn = 1,
        Tracking = 2,
        Releasing = 3
    }

    public readonly struct HandIkSample
    {
        public HandIkSample(HandIkState state, float weight)
        {
            State = state;
            Weight = weight < 0f ? 0f : weight > 1f ? 1f : weight;
        }

        public HandIkState State { get; }
        public float Weight { get; }
    }

    public static class HandIkSolver
    {
        public static HandIkSample Step(
            HandIkState state,
            float currentWeight,
            float targetWeight,
            float deltaTime,
            float blendSeconds,
            float releaseSeconds)
        {
            float target = Clamp01(targetWeight);
            float response = target > currentWeight ? blendSeconds : releaseSeconds;
            float step = deltaTime / Max(0.01f, response);
            float weight = MoveTowards(Clamp01(currentWeight), target, step);
            HandIkState next = target > 0f
                ? weight + 0.0001f >= target ? HandIkState.Tracking : HandIkState.BlendingIn
                : weight <= 0.0001f ? HandIkState.Inactive : HandIkState.Releasing;
            return new HandIkSample(next, weight);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Max(float a, float b) => a > b ? a : b;
        private static float MoveTowards(float current, float target, float maximumDelta)
        {
            if (current < target) return current + maximumDelta > target ? target : current + maximumDelta;
            return current - maximumDelta < target ? target : current - maximumDelta;
        }
    }
}
