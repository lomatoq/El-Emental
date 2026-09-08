using Unity.Mathematics;
namespace Elemental.Simulation.Bending
{
    public static class EarthWallPushContactPolicy
    {
        public static bool CanTipSlidingWall(float mass,float impulse)=>mass>0&&math.isfinite(impulse)&&impulse>mass*6f;
        public static bool CanPloughDecor(float wallMass,float rockMass,float speed)=>wallMass>0&&rockMass>0&&rockMass<wallMass*.65f&&speed>1.5f;
        public static float PloughRemainingSpeed(float wallMass,float rockMass,float speed)=>speed*wallMass/(wallMass+rockMass*2);
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
