using Elemental.Simulation.Bending;
using UnityEngine;
namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private readonly int[] _heavyChipPieces={-1,-1,-1};
        private readonly Vector3[] _heavyChipOffsets=new Vector3[3];
        private float _heavyChipStarted;
        public int HeavyContactChippedPieceCount {get;private set;}
        public float MaximumHeavyContactOffsetMeters {get;private set;}
        private void ResetHeavyContactChips()
        {
            HeavyContactChippedPieceCount=0;MaximumHeavyContactOffsetMeters=0;
            for(int i=0;i<3;i++){_heavyChipPieces[i]=-1;_heavyChipOffsets[i]=Vector3.zero;}
        }
        private Vector3 HeavyContactChipOffset(int piece)
        {
            for(int i=0;i<HeavyContactChippedPieceCount;i++)
                if(_heavyChipPieces[i]==piece)
                    return _heavyChipOffsets[i]*Mathf.SmoothStep(0,1,(Time.time-_heavyChipStarted)/.12f);
            return Vector3.zero;
        }
        private bool TryChipAgainstHeavyObstacle(Collision collision)
        {
            if(!_heldPushCoasting||_fractured||_body==null||_body.isKinematic||collision.contactCount==0)return false;
            var other=collision.rigidbody;
            bool fixedObstacle=other==null||other.isKinematic;
            if(!fixedObstacle&&Vector3.Dot(other.linearVelocity,_heldPushDirection)<-2)return false;
            float speed=Vector3.Dot(_pushContactVelocity,_heldPushDirection);
            Vector3 point=Vector3.zero,normal=Vector3.zero;int contacts=0;
            for(int i=0;i<collision.contactCount;i++)
            {
                var contact=collision.GetContact(i);
                if(!EarthWallPushContactPolicy.ShouldChipHeavyObstacle(_body.mass,other!=null?other.mass:0,
                    fixedObstacle,speed,Vector3.Dot(contact.normal,_up),Vector3.Dot(contact.normal,_heldPushDirection)))continue;
                // A low floor seam is support, not a heavy frontal strike.
                float contactHeight=Vector3.Dot(contact.point-_pushContactCenter,_up)+Height*.5f;
                if(contactHeight<Mathf.Max(.2f,Height*.25f))continue;
                point+=contact.point;normal+=contact.normal;contacts++;
            }
            if(contacts==0)return false;
            point/=contacts;normal.Normalize();
            _body.linearVelocity=Vector3.zero;_body.angularVelocity=Vector3.zero;
            StopHeldPushCoasting();BeginCohesiveFracture();
            ResetHeavyContactChips();_heavyChipStarted=Time.time;
            int nearest=-1;float distance=float.PositiveInfinity;
            for(int i=0;i<_pieces.Length;i++)
            {
                if(!_pieceAnchored[i]||_pieces[i]==null)continue;
                float d=(_pieces[i].position-point).sqrMagnitude;
                if(d<distance){distance=d;nearest=i;}
            }
            if(nearest<0)return true;
            _heavyChipPieces[0]=nearest;HeavyContactChippedPieceCount=1;
            for(int cursor=0;cursor<HeavyContactChippedPieceCount&&HeavyContactChippedPieceCount<3;cursor++)
                for(int b=0;b<_bonds.Length&&HeavyContactChippedPieceCount<3;b++)
                {
                    var bond=_bonds[b];if(_bondBroken[b]||bond.Foundation)continue;
                    int next=bond.PieceA==_heavyChipPieces[cursor]?bond.PieceB:bond.PieceB==_heavyChipPieces[cursor]?bond.PieceA:-1;
                    if(next<0||!_pieceAnchored[next])continue;
                    bool used=false;for(int j=0;j<HeavyContactChippedPieceCount;j++)if(_heavyChipPieces[j]==next)used=true;
                    if(!used)_heavyChipPieces[HeavyContactChippedPieceCount++]=next;
                }
            for(int i=0;i<HeavyContactChippedPieceCount;i++)
            {
                // Shift away from the blocker; existing interior/foundation bonds remain.
                float amount=EarthWallPushContactPolicy.ChipDisplacement(_heldPushReleasedCharge)*(1-i*.15f);
                _heavyChipOffsets[i]=normal*amount;MaximumHeavyContactOffsetMeters=Mathf.Max(MaximumHeavyContactOffsetMeters,amount);
            }
            for(int i=0;i<_pieceBodies.Length;i++)SynchronizeSupportedPiece(i);
            return true;
        }
    }
}