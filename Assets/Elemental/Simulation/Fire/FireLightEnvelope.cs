using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public static class FireLightEnvelope
    {
        public static float Sample(float time,float seed,float heat=1)
        {
            if(!math.isfinite(time)||!math.isfinite(seed)||!math.isfinite(heat))return 0;
            return math.saturate(heat)*(1+.13f*math.sin(time*5.3f+seed)+.07f*math.sin(time*9.7f+seed*2.31f));
        }
    }
}
