using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public static class FireWeaveEmissionOwnership
    {
        // Smooth geometry is not permission to keep injecting from a former ability's nozzle.
        public static float SourceOrbitBlend(FireWeaveForm form,float smoothedCurl)
            =>form>=FireWeaveForm.Orbit?1:math.saturate(math.isfinite(smoothedCurl)?smoothedCurl:0);
    }
}
