using Unity.Mathematics;

namespace Elemental.Simulation.Networking
{
    /// <summary>Pause cancels held input once; later neutral frames cannot replay releases or tap-jumps.</summary>
    public static class EarthSemanticInputSuppression
    {
        public static EarthSemanticInputFrame Apply(EarthSemanticInputFrame frame, EarthInputBits previousHeld, bool beginning)
        {
            frame.Held = EarthInputBits.None;
            frame.Pressed = beginning ? EarthInputBits.Cancel : EarthInputBits.None;
            frame.Released = beginning ? previousHeld : EarthInputBits.None;
            frame.Move = float2.zero; frame.Scroll = 0;
            return frame;
        }
    }
}
