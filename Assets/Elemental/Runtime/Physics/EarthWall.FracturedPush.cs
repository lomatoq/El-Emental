using System;
using UnityEngine;

namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private struct FracturedPushPose
        {
            public bool Selected, Kinematic, Supported;
            public Vector3 Position, Velocity, AngularVelocity;
            public Quaternion Rotation;
            public CollisionDetectionMode CollisionMode;
        }
        private FracturedPushPose[] _fracturedPushPoses=Array.Empty<FracturedPushPose>();
        private int[] _fracturedPushComponents=Array.Empty<int>();
        private int[] _fracturedPushQueue=Array.Empty<int>();
        private Vector3 _fracturedPushDirection;
        public bool IsFracturedHeldPushActive { get; private set; }
        public float FracturedHeldPushMass { get; private set; }
        public bool CanBeginFracturedHeldPush
        {
            get
            {
                if(!isActiveAndEnabled||!_fractured||_pieceBodies==null||_bonds==null)return false;
                for(int i=0;i<_pieceBodies.Length;i++)
                    if(IsLiveFracturedPushPiece(i)&&_cohesion!=null&&_cohesion.IsPieceHeld(i))return false;
                for(int i=0;i<_bonds.Length;i++)
                    if(!_bondBroken[i]&&!_bonds[i].Foundation&&IsLiveFracturedPushPiece(_bonds[i].PieceA)&&IsLiveFracturedPushPiece(_bonds[i].PieceB))return true;
                return false;
            }
        }
        private bool IsLiveFracturedPushPiece(int i)=>i>=0&&i<_pieceBodies.Length&&_pieceBodies[i]!=null&&
            _pieces[i]!=null&&_pieces[i].gameObject.activeInHierarchy&&_pieceBodies[i].detectCollisions;

        public bool BeginFracturedHeldPush(Vector3 direction)
        {
            if(IsFracturedHeldPushActive)return true;
            if(!CanBeginFracturedHeldPush)return false;
            int count=_pieceBodies.Length;
            if(_fracturedPushPoses.Length!=count)
            {
                _fracturedPushPoses=new FracturedPushPose[count];
                _fracturedPushComponents=new int[count];_fracturedPushQueue=new int[count];
            }
            Array.Clear(_fracturedPushPoses,0,count);
            for(int i=0;i<count;i++)_fracturedPushComponents[i]=-1;
            int component=0,best=-1;float bestMass=0;
            for(int seed=0;seed<count;seed++)
            {
                if(!IsLiveFracturedPushPiece(seed)||_fracturedPushComponents[seed]>=0)continue;
                int head=0,tail=1;float mass=0;_fracturedPushQueue[0]=seed;_fracturedPushComponents[seed]=component;
                while(head<tail)
                {
                    int piece=_fracturedPushQueue[head++];mass+=_pieceBodies[piece].mass;
                    for(int b=0;b<_bonds.Length;b++)
                    {
                        var bond=_bonds[b];if(_bondBroken[b]||bond.Foundation)continue;
                        int next=bond.PieceA==piece?bond.PieceB:bond.PieceB==piece?bond.PieceA:-1;
                        if(next<0||!IsLiveFracturedPushPiece(next)||_fracturedPushComponents[next]>=0)continue;
                        _fracturedPushComponents[next]=component;_fracturedPushQueue[tail++]=next;
                    }
                }
                if(tail>=2&&mass>bestMass){best=component;bestMass=mass;}
                component++;
            }
            if(best<0)return false;
            _fracturedPushDirection=Vector3.ProjectOnPlane(direction,_up).normalized;
            if(_fracturedPushDirection.sqrMagnitude<.5f)_fracturedPushDirection=_forward;
            FracturedHeldPushMass=bestMass;
            for(int i=0;i<count;i++)
            {
                if(_fracturedPushComponents[i]!=best)continue;
                var body=_pieceBodies[i];bool kinematic=body.isKinematic;
                _fracturedPushPoses[i]=new FracturedPushPose { Selected=true,Kinematic=kinematic,
                    Supported=_pieceAnchored[i],Position=body.position,Rotation=body.rotation,
                    Velocity=kinematic?Vector3.zero:body.linearVelocity,
                    AngularVelocity=kinematic?Vector3.zero:body.angularVelocity,CollisionMode=body.collisionDetectionMode };
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.isKinematic=true;
            }
            IsFracturedHeldPushActive=true;return true;
        }
        public void TickFracturedHeldPush()
        {
            if(!IsFracturedHeldPushActive)return;
            if(!_fractured){CancelFracturedHeldPush();return;}
            for(int i=0;i<_fracturedPushPoses.Length;i++)
            {
                var pose=_fracturedPushPoses[i];if(!pose.Selected||!IsLiveFracturedPushPiece(i))continue;
                // Another spell acquiring any selected child cancels this ownership.
                if(_cohesion!=null&&_cohesion.IsPieceHeld(i)){CancelFracturedHeldPush();return;}
                var body=_pieceBodies[i];body.isKinematic=true;body.position=pose.Position;body.rotation=pose.Rotation;
            }
        }
        public void CancelFracturedHeldPush()
        {
            if(!IsFracturedHeldPushActive)return;
            IsFracturedHeldPushActive=false;
            for(int i=0;i<_fracturedPushPoses.Length;i++)
            {
                var pose=_fracturedPushPoses[i];if(!pose.Selected||!IsLiveFracturedPushPiece(i))continue;
                if(_cohesion!=null&&_cohesion.IsPieceHeld(i))continue;
                var body=_pieceBodies[i];
                // A bond broken during charge must not resurrect an old support state.
                body.isKinematic=pose.Kinematic&&(!pose.Supported||_pieceAnchored[i]);
                body.collisionDetectionMode=pose.CollisionMode;
                if(!body.isKinematic){body.linearVelocity=pose.Velocity;body.angularVelocity=pose.AngularVelocity;}
            }
        }
        public bool ReleaseFracturedHeldPush(float impulse)
        {
            if(!IsFracturedHeldPushActive)return false;
            if(!_fractured||!float.IsFinite(impulse)||impulse<=0){CancelFracturedHeldPush();return false;}
            for(int i=0;i<_fracturedPushPoses.Length;i++)
                if(_fracturedPushPoses[i].Selected&&_cohesion!=null&&_cohesion.IsPieceHeld(i)){CancelFracturedHeldPush();return false;}
            CancelFracturedHeldPush();
            // Preserve every surviving interior bond and all existing damage.
            // Only ground anchoring must release for this existing component to move.
            for(int b=0;b<_bonds.Length;b++)
                if(!_bondBroken[b]&&_bonds[b].Foundation&&_fracturedPushPoses[_bonds[b].PieceA].Selected)
                    ReleaseBond(b,0,transform.position,_fracturedPushDirection);
            RecomputeConnectivity();
            float mass=0;
            for(int i=0;i<_fracturedPushPoses.Length;i++)
                if(_fracturedPushPoses[i].Selected&&IsLiveFracturedPushPiece(i))mass+=_pieceBodies[i].mass;
            if(mass<=0)return false;
            for(int i=0;i<_fracturedPushPoses.Length;i++)
            {
                if(!_fracturedPushPoses[i].Selected||!IsLiveFracturedPushPiece(i))continue;
                var body=_pieceBodies[i];body.isKinematic=false;
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                body.AddForce(_fracturedPushDirection*(impulse*body.mass/mass),ForceMode.Impulse);body.WakeUp();
            }
            return true;
        }
    }
}
