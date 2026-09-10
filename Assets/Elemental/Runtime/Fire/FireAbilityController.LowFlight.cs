using UnityEngine;
namespace Elemental.Runtime.Fire
{
    public sealed partial class FireAbilityController
    {
        private float nextLowFlightWake;
        public bool IsLowFlying=>IsAvailable&&motor!=null&&motor.FireLowFlightActive;
        public Vector3 LowFlightDirection=>motor!=null?motor.FireLowFlightDirection:Vector3.forward;
        public float LowFlightSpeed01=>Mathf.Clamp01(Vector3.ProjectOnPlane(OwnerVelocity,LocalUp).magnitude/
            Elemental.Simulation.Characters.FireLowFlightMotion.MaximumSpeed);
        public void SetLowFlightHeld(bool held)
        {
            bool was=IsLowFlying;motor?.SetFireLowFlight(held&&IsAvailable&&!liftHeld&&!IsRingCharging&&!IsBoltCharging);
            if(IsLowFlying&&!was){BeginContour();nextLowFlightWake=clock;}
        }
        private void StepLowFlight()
        {
            if(!IsLowFlying||clock<nextLowFlightWake)return;
            nextLowFlightWake=clock+.07f;
            // Reuse generation-checked, oldest-first short sources. The wake has actual heat and soot authority.
            Vector3 wake=motor.SupportFeetPoint(LocalUp)-LowFlightDirection*.28f;
            if(TryGround(wake,LocalUp,out RaycastHit hit))TraceContour(hit.point);
        }
    }
}
