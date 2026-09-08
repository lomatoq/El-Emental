using UnityEngine;
namespace Elemental.Runtime.Physics
{
    public sealed class EarthRigidDomainTarget:MonoBehaviour,IEarthPhysicalTarget
    {
        private IEarthPhysicalTarget _target;
        public IEarthPhysicalTarget Source => _target;
        public void Configure(IEarthPhysicalTarget target)=>_target=target;
        public Rigidbody Body=>_target.Body;
        public uint StableEarthId=>_target.StableEarthId;
        public EarthPhysicalTargetHandle TargetHandle=>_target.TargetHandle;
        public float EarthMass=>_target.EarthMass;
        public EarthPhysicalTargetKind TargetKind=>_target.TargetKind;
        public bool IsEarthTargetValid=>_target!=null&&_target.IsEarthTargetValid;
        public void OnEarthMagicGrabbed(EarthMagicGripKind grip)=>_target.OnEarthMagicGrabbed(grip);
        public void OnEarthMagicReleased(EarthMagicGripKind grip)=>_target.OnEarthMagicReleased(grip);
    }
}
