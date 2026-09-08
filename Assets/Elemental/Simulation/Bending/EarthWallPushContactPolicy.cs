using Unity.Mathematics;
namespace Elemental.Simulation.Bending
{
    public static class EarthWallPushContactPolicy
    {
        public static float ChipDisplacement(float charge)=>math.lerp(.012f,.025f,math.saturate(charge));
        public static bool ShouldChipHeavyObstacle(float wallMass,float otherMass,bool fixedObstacle,float forwardSpeed,float normalUp,float normalForward)=>
            wallMass>0&&forwardSpeed>2&&normalUp<.65f&&normalForward<-.35f&&(fixedObstacle||otherMass>=wallMass*1.5f);
        public static bool IsOutgoingLooseStone(float wallMass,float stoneMass,float wallForwardSpeed,
            float stoneForwardSpeed,float impulse)
        {
            if(!math.isfinite(wallMass)||!math.isfinite(stoneMass)||!math.isfinite(wallForwardSpeed)||
                !math.isfinite(stoneForwardSpeed)||!math.isfinite(impulse)||wallMass<=0||stoneMass<=0||impulse<0)return false;
            if(stoneMass>wallMass*.35f||wallForwardSpeed<.5f||
                stoneForwardSpeed < -math.max(.75f,wallForwardSpeed*.15f))return false;
            // PhysX already transfers momentum. Only classify contacts whose
            // impulse fits the launched wall's finite outgoing collision budget.
            float reducedMass=wallMass*stoneMass/(wallMass+stoneMass);
            return impulse <= reducedMass*wallForwardSpeed*2f+1f;
        }
    }
}
