using Elemental.Simulation.Bending;
using UnityEngine;
namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private Vector3 _pushContactVelocity;
        private Vector3 _pushContactCenter;
        private float _pushContactTick=-1;
        private float _pendingPushContactSpeed;
        public int OutgoingLooseStoneContacts { get; private set; }

        internal void RecordHeldPushLaunch(float impulse)
        {
            if(_body==null||!float.IsFinite(impulse)||impulse<=0)return;
            _pendingPushContactSpeed=Mathf.Max(0,Vector3.Dot(_body.linearVelocity,_heldPushDirection))+
                impulse/Mathf.Max(.01f,_body.mass);
            _pushContactVelocity=_heldPushDirection*_pendingPushContactSpeed;
            _pushContactCenter=_body.worldCenterOfMass;
            _pushContactTick=Time.fixedTime;
        }

        private void CachePushContactVelocity()
        {
            _pushContactTick=Time.fixedTime;
            if(_body!=null)_pushContactCenter=_body.worldCenterOfMass;
            _pushContactVelocity=_heldPushCoasting&&_body!=null&&!_body.isKinematic
                ? _body.linearVelocity : Vector3.zero;
            if(_heldPushCoasting&&_pendingPushContactSpeed>0)
            {
                // AddForce(Impulse) can still be queued before this simulation.
                // Use its one-step finite prediction, never add it to observed
                // velocity again if PhysX already consumed the impulse.
                float forward=Vector3.Dot(_pushContactVelocity,_heldPushDirection);
                _pushContactVelocity+=_heldPushDirection*Mathf.Max(0,_pendingPushContactSpeed-forward);
            }
            _pendingPushContactSpeed=0;
        }

        private bool TryHandleOutgoingPushContact(Collision collision)
        {
            if(collision==null||collision.contactCount==0)return false;
            var contact=collision.GetContact(0);
            if(Vector3.Dot(contact.normal,_heldPushDirection)>-.35f)return false;
            return TryHandleOutgoingPushStone(collision.rigidbody,contact.point,collision.impulse.magnitude);
        }

        // Shared with incoming fragment callbacks, whose delivery order relative
        // to the wall callback is unspecified. No impulse or collision is removed.
        private bool TryHandleOutgoingPushStone(Rigidbody stone,Vector3 point,float impulse)
        {
            if(!_heldPushCoasting||_fractured||_body==null||_body.isKinematic||stone==null||stone==_body||
                stone.isKinematic||Mathf.Abs(Time.fixedTime-_pushContactTick)>.001f)return false;
            if(stone.GetComponent<EarthWall>()!=null||stone.GetComponent<EarthWallPiece>()!=null||
                stone.GetComponent<EarthPlatform>()!=null||stone.GetComponent<EarthArenaStructure>()!=null)return false;
            bool looseStone=stone.GetComponent<EarthFragment>()!=null||stone.GetComponent<EarthRockDebris>()!=null||
                stone.GetComponent<EarthArenaPiece>()!=null||stone.GetComponent<PhysicalImpactTarget>()!=null;
            if(!looseStone||EarthBodyTargetFilter.IsCharacterBody(stone))return false;
            // Contact belongs to the pre-step shape. The post-solver body may
            // already have moved beyond that point during a fast first shove.
            if(Vector3.Dot(point-_pushContactCenter,_heldPushDirection)<Mathf.Max(.01f,Thickness*.15f))return false;
            if(!EarthWallPushContactPolicy.IsOutgoingLooseStone(_body.mass,stone.mass,
                Vector3.Dot(_pushContactVelocity,_heldPushDirection),
                Vector3.Dot(stone.linearVelocity,_heldPushDirection),impulse))return false;
            OutgoingLooseStoneContacts++;
            RevealCracks();
            return true;
        }
    }
}
