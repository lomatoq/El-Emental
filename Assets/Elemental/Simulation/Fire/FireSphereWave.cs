using Unity.Mathematics;
namespace Elemental.Simulation.Fire
{
    public static class FireSphereWave
    {
        public const float Duration=.9f,StartRadius=2.3f,MaximumRadius=9f,RecoverySeconds=1.4f;
        public const int ContactCapacity=512,ContactsPerStep=64;
        public static float Radius(float age)
        {float t=math.saturate(age/Duration);return math.lerp(StartRadius,MaximumRadius,1-(1-t)*(1-t));}
        public static float Strength(float distance)=>math.lerp(1,.55f,math.saturate(distance/MaximumRadius));
    }
}
