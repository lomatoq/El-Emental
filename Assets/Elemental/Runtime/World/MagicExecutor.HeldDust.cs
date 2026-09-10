using Elemental.Runtime.Physics;
using Elemental.Simulation.Bending;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.World
{
    public sealed partial class MagicExecutor
    {
        private static readonly ProfilerMarker HeldDustMarker=new("Elemental.Earth.HeldMaterialShed");
        private float _nextHeldMaterialShed;
        private int _heldShedCursor;
        public int HeldMaterialShedEvents {get;private set;}
        private void EmitHeldMaterialShed()
        {
            if(materialFeedback==null||Time.timeScale<=0||Time.time<_nextHeldMaterialShed)return;
            bool gravity=_gravityWellActive&&_repairController==null&&_gravityStructureIntent!=EarthGravityStructureIntent.Repair;
            if(!gravity&&!HasHeldFractureCluster&&HeldBody==null&&VectorFieldBody==null)return;
            using var marker=HeldDustMarker.Auto();
            _nextHeldMaterialShed=Time.time+.16f;
            if(gravity&&_gravityGripSession.Count>0)
            {
                int count=_gravityGripSession.Count;
                for(int i=0;i<Mathf.Min(3,count);i++)
                {
                    var target=_gravityGripSession.GetTarget((_heldShedCursor++)%count);
                    if(target!=null&&target.IsEarthTargetValid)EmitBodyShed(target.Body,target.StableEarthId,target.TargetHandle.Generation);
                }
            }
            else if(HasHeldFractureCluster)
            {
                for(int i=0;i<Mathf.Min(3,_heldFractureCount);i++)
                {
                    var fragment=_heldFractureCluster[(_heldShedCursor++)%_heldFractureCount];
                    if(fragment!=null)EmitBodyShed(fragment.Body,fragment.FragmentId,0);
                }
            }
            else
            {
                Rigidbody held=HeldBody;
                if(held!=null)EmitBodyShed(held,HeldFragment!=null?HeldFragment.FragmentId:0,0);
                if(VectorFieldBody!=null&&VectorFieldBody!=held)EmitBodyShed(VectorFieldBody,_vectorFieldTarget.StableEarthId,_vectorFieldTarget.TargetHandle.Generation);
            }
        }
        private void EmitBodyShed(Rigidbody body,uint source,uint generation)
        {
            if(body==null||!body.gameObject.activeInHierarchy)return;
            Collider shape=body.GetComponent<Collider>();if(shape==null||!shape.enabled)return;
            Vector3 center=body.worldCenterOfMass;
            Vector3 up=SafeDirection(center-(planetCenter!=null?planetCenter.position:Vector3.zero));
            Vector3 tangent=Vector3.Cross(up,Mathf.Abs(up.y)<.9f?Vector3.up:Vector3.forward).normalized;
            float angle=(_heldShedCursor+HeldMaterialShedEvents)*2.399963f;
            Vector3 outward=(-up*.65f+(tangent*Mathf.Cos(angle)+Vector3.Cross(up,tangent)*Mathf.Sin(angle))*.76f).normalized;
            Vector3 point=shape.ClosestPoint(shape.bounds.center+outward*(shape.bounds.extents.magnitude+1))+outward*.06f;
            materialFeedback.Emit(EarthMaterialFeedbackKind.AirborneShed,point,up,1f,.18f,source,generation,10,3);
            HeldMaterialShedEvents++;
        }
    }
}
