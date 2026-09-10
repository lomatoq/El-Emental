using Elemental.Simulation.Fire;
using Unity.Profiling;
using UnityEngine;
namespace Elemental.Runtime.Fire
{
    public sealed partial class FireAbilityController
    {
        private static readonly ProfilerMarker ChargedPoseMarker=new("Elemental.Fire.ChargedMuzzle");
        public bool TryGetChargedBoltPose(out Vector3 center,out Vector3 direction,out float radius)
        {
            center=default;direction=default;radius=0;
            if(!IsAvailable||!IsBoltCharging||hand==null)return false;
            var profile=FireChargedBoltProfile.Evaluate(BoltCharge01);
            center=ChargedBoltMuzzle(chargedAim,profile,out direction);radius=profile.VisualRadius;return true;
        }
        // Same bounded sweep is used while holding and when the projectile is released.
        private Vector3 ChargedBoltMuzzle(Vector3 aim,FireChargedBoltProfile profile,out Vector3 direction)
        {
            using var marker=ChargedPoseMarker.Auto();
            Vector3 origin=hand.position;direction=(aim-origin).normalized;
            if(direction.sqrMagnitude<.9f)return origin;
            Vector3 candidate=origin+direction*(profile.VisualRadius+.08f);
            return ClearMuzzle(origin,candidate,profile.CollisionRadius)?candidate:origin;
        }
    }
}
