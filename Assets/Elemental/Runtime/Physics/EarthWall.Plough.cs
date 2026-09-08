using Elemental.Simulation.Bending;
using UnityEngine;
using Unity.Profiling;
namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private static readonly ProfilerMarker RigidSlideMarker=new("Elemental.Earth.Wall.RigidSlide");
        private float _ploughContactAt=-10;
        private uint _ploughRockId;
        private bool _fracturedSlide;
        private float _fracturedBeforeSpeed;
        public int PloughedDecorContacts {get;private set;}
        public int PloughDetachedDomains {get;private set;}
        private bool TryPloughDecor(Collision collision)
        {
            if(collision==null||collision.contactCount==0||(!_heldPushCoasting&&!_fracturedSlide))return false;
            var rock=collision.collider.GetComponentInParent<EarthDestructibleDecorRock>();
            if(rock==null)return false;
            var contact=collision.GetContact(0);
            if(Vector3.Dot(contact.normal,_heldPushDirection)>-.25f)return false;
            float mass=_fractured?FracturedHeldPushMass:_body.mass;
            float speed=_fractured?Mathf.Max(_fracturedBeforeSpeed,FracturedSlideSpeed()):Vector3.Dot(_pushContactVelocity,_heldPushDirection);
            if(!EarthWallPushContactPolicy.CanPloughDecor(mass,rock.EarthMass,speed))return false;
            if(_ploughRockId==rock.StableEarthId&&Time.time-_ploughContactAt<.25f)return true;
            _ploughRockId=rock.StableEarthId;_ploughContactAt=Time.time;
            float remaining=EarthWallPushContactPolicy.PloughRemainingSpeed(mass,rock.EarthMass,speed);
            rock.ApplyImpact(contact.point,_heldPushDirection,Mathf.Min(mass*speed*.3f,rock.EarthMass*speed*2));
            if(rock.IsAnchored&&!rock.IsShattered)return false;
            PloughedDecorContacts++;
            if(!_fractured)
            {
                _body.linearVelocity=Vector3.zero;StopHeldPushCoasting();BeginCohesiveFracture();
            }
            ReleaseRigidSlideCarrier();
            int first=-1;
            for(int chip=0;chip<(_heldPushReleasedCharge>.7f?2:1);chip++)
            {
                int nearest=-1;float best=float.PositiveInfinity;
                for(int i=0;i<_pieces.Length;i++)
                {
                    if(i==first||!IsLiveFracturedPushPiece(i))continue;
                    bool linked=false;
                    for(int b=0;b<_bonds.Length;b++)if(!_bondBroken[b]&&(_bonds[b].PieceA==i||(!_bonds[b].Foundation&&_bonds[b].PieceB==i))){linked=true;break;}
                    float distance=(_pieces[i].position-contact.point).sqrMagnitude;
                    if(linked&&distance<best){nearest=i;best=distance;}
                }
                if(nearest<0)break;
                if(chip==0)first=nearest;
                for(int b=0;b<_bonds.Length;b++)if(!_bondBroken[b]&&(_bonds[b].PieceA==nearest||(!_bonds[b].Foundation&&_bonds[b].PieceB==nearest)))
                    ReleaseBond(b,0,contact.point,_heldPushDirection);
                PloughDetachedDomains++;
            }
            RecomputeConnectivity();
            if(BeginFracturedHeldPush(_heldPushDirection))
            {
                for(int i=0;i<_pieceBodies.Length;i++)if(_pieceBodies[i]!=null&&!_pieceBodies[i].isKinematic)_pieceBodies[i].linearVelocity=Vector3.zero;
                for(int i=0;i<_fracturedPushPoses.Length;i++)if(_fracturedPushPoses[i].Selected)_fracturedPushPoses[i].Velocity=Vector3.zero;
                ReleaseFracturedHeldPush(remaining*FracturedHeldPushMass);
                _heldPushFracturedFeedback=true;
            }
            materialFeedback?.Emit(EarthMaterialFeedbackKind.Fracture,contact.point,_up,1.5f,1,WallId,_generation,180,36);
            return true;
        }
        private float FracturedSlideSpeed()
        {
            float mass=0,speed=0;
            for(int i=0;i<_fracturedPushPoses.Length;i++)
                if(_fracturedPushPoses[i].Selected&&IsLiveFracturedPushPiece(i)&&!_pieceBodies[i].isKinematic)
                {mass+=_pieceBodies[i].mass;speed+=Vector3.Dot(_pieceBodies[i].linearVelocity,_heldPushDirection)*_pieceBodies[i].mass;}
            return mass>0?speed/mass:0;
        }
        private void StabilizeFracturedSlide()
        {
            if(!_fracturedSlide||!_fractured||IsFracturedHeldPushActive)return;
            using var measured=RigidSlideMarker.Auto();
            if(_rigidCarrierActive)
                for(int i=0;i<_carrierMasses.Length;i++)
                    if(IsRigidSlideFollower(i))
                    {
                        _pieceBodies[i].position=_carrierShapes[i].transform.position;
                        _pieceBodies[i].rotation=_carrierShapes[i].transform.rotation;
                    }
            float mass=0;Vector3 average=Vector3.zero,center=Vector3.zero,angular=Vector3.zero;
            for(int i=0;i<_fracturedPushPoses.Length;i++)
                if(_fracturedPushPoses[i].Selected&&IsLiveFracturedPushPiece(i)&&!_pieceBodies[i].isKinematic)
                                {
                    if(_cohesion!=null&&_cohesion.IsPieceHeld(i)){ReleaseRigidSlideCarrier();_fracturedSlide=false;return;}
                    mass+=_pieceBodies[i].mass;average+=_pieceBodies[i].linearVelocity*_pieceBodies[i].mass;
                    center+=_pieceBodies[i].worldCenterOfMass*_pieceBodies[i].mass;
                    angular+=_pieceBodies[i].angularVelocity*_pieceBodies[i].mass;
                }
            if(mass<=0){_fracturedSlide=false;return;}
            average/=mass;center/=mass;angular/=mass;
            _fracturedBeforeSpeed=Vector3.Dot(average,_heldPushDirection);
            // Remove collective lift, not rotational point velocity: a rigid island may tip.
            float outward=Mathf.Max(0,Vector3.Dot(average,_up));
            for(int i=0;i<_fracturedPushPoses.Length;i++)
            {
                if(!_fracturedPushPoses[i].Selected||!IsLiveFracturedPushPiece(i))continue;
                var body=_pieceBodies[i];if(body.isKinematic)continue;
                // One rigid velocity field removes elastic internal modes; contacts still solve normally.
                body.linearVelocity=average-_up*outward+Vector3.Cross(angular,body.worldCenterOfMass-center);
                body.angularVelocity=angular;
                body.AddForce(-_up*6,ForceMode.Acceleration);
            }
        }
    }
}
