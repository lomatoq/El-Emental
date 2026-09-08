using UnityEngine;
namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private int _rigidCarrier=-1;
        private bool _rigidCarrierActive, _rigidSlideCanTip;
        private RigidbodyConstraints _carrierConstraints;
        private float[] _carrierMasses;
        private Transform[] _carrierParents;
        private Vector3[] _carrierCenters;
        private bool[] _carrierColliderEnabled,_carrierRendererEnabled;
        private MeshCollider[] _carrierShapes;
        public Vector3 RigidDomainPosition(int index)=>_rigidCarrierActive&&index!=_rigidCarrier&&_carrierMasses[index]>0?_carrierShapes[index].transform.position:_pieceBodies[index].transform.position;
        public bool HasRigidSlideCarrier=>_rigidCarrier>=0;
        public Rigidbody RigidSlideBody=>HasRigidSlideCarrier?_pieceBodies[_rigidCarrier]:null;
        public bool IsRigidDomain(int index)=>_rigidCarrierActive&&_carrierMasses[index]>0;
        internal float DomainMass(int index,float bodyMass)=>IsRigidDomain(index)?_carrierMasses[index]:bodyMass;
        private void ReleaseRigidSlideCarrier()
        {
            if(!_rigidCarrierActive)return;
            var carrier=_pieceBodies[_rigidCarrier];
            Vector3 center=carrier.worldCenterOfMass,velocity=carrier.linearVelocity,angular=carrier.angularVelocity;
            _rigidCarrierActive=false;
            for(int i=0;i<_carrierMasses.Length;i++)
            {
                if(_carrierMasses[i]<=0)continue;
                var body=_pieceBodies[i];if(body==null)continue;
                Transform pose=i!=_rigidCarrier&&_carrierShapes[i]!=null?_carrierShapes[i].transform:body.transform;
                Vector3 point=pose.position;Quaternion rotation=pose.rotation;
                if(body.transform.parent!=_carrierParents[i])body.transform.SetParent(_carrierParents[i],true);
                body.position=point;body.rotation=rotation;body.mass=_carrierMasses[i];body.centerOfMass=_carrierCenters[i];
                body.detectCollisions=true;body.isKinematic=false;
                _pieceColliders[i].enabled=_carrierColliderEnabled[i];
                body.GetComponent<MeshRenderer>().enabled=_carrierRendererEnabled[i];
                if(_carrierShapes[i]!=null)_carrierShapes[i].gameObject.SetActive(false);
                body.linearVelocity=velocity+Vector3.Cross(angular,body.worldCenterOfMass-center);body.angularVelocity=angular;
                _carrierMasses[i]=0;
            }
            carrier.constraints=_carrierConstraints;carrier.ResetInertiaTensor();_rigidCarrier=-1;
            for(int b=0;b<_bonds.Length;b++)if(!_bondBroken[b]&&!_bonds[b].Foundation)
            {
                var bond=_bonds[b];var runtime=_structureRuntime?.GetBondRuntime(b);
                if(runtime!=null)runtime.Activate(_pieceBodies[bond.PieceB]);
                else SetWeldMotion(bond.Joint,ConfigurableJointMotion.Locked);
            }
        }
        private static void SetWeldMotion(ConfigurableJoint joint,ConfigurableJointMotion motion)
        {
            joint.xMotion=joint.yMotion=joint.zMotion=motion;
            joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=motion;
        }
        private void BuildRigidSlideCarrier()
        {
            ReleaseRigidSlideCarrier();
            int count=_pieceBodies.Length;
            if(_carrierMasses==null||_carrierMasses.Length!=count)
            {_carrierMasses=new float[count];_carrierParents=new Transform[count];_carrierCenters=new Vector3[count];_carrierColliderEnabled=new bool[count];_carrierRendererEnabled=new bool[count];_carrierShapes=new MeshCollider[count];}
            int leader=-1;float mass=0;Vector3 center=Vector3.zero,velocity=Vector3.zero,angular=Vector3.zero;
            for(int i=0;i<count;i++)if(_fracturedPushPoses[i].Selected&&IsLiveFracturedPushPiece(i))
            {
                var body=_pieceBodies[i];float m=body.mass;
                if(leader<0||m>_pieceBodies[leader].mass)leader=i;
                mass+=m;center+=body.worldCenterOfMass*m;velocity+=body.linearVelocity*m;angular+=body.angularVelocity*m;
                _carrierMasses[i]=m;_carrierCenters[i]=body.centerOfMass;_carrierParents[i]=body.transform.parent;_carrierColliderEnabled[i]=_pieceColliders[i].enabled;_carrierRendererEnabled[i]=body.GetComponent<MeshRenderer>().enabled;
            }
            if(leader<0)return;
            var carrier=_pieceBodies[leader];center/=mass;velocity/=mass;angular/=mass;
            for(int b=0;b<_bonds.Length;b++)if(!_bondBroken[b]&&!_bonds[b].Foundation&&_carrierMasses[_bonds[b].PieceA]>0&&_carrierMasses[_bonds[b].PieceB]>0)
                SetWeldMotion(_bonds[b].Joint,ConfigurableJointMotion.Free);
            for(int i=0;i<count;i++)
            {
                if(i==leader||_carrierMasses[i]<=0)continue;
                var body=_pieceBodies[i];var source=_pieceColliders[i] as MeshCollider;
                var proxy=_carrierShapes[i];
                if(proxy==null)
                {
                    var go=new GameObject("Rigid wall domain contact");proxy=_carrierShapes[i]=go.AddComponent<MeshCollider>();
                    go.AddComponent<EarthRigidDomainTarget>().Configure(_pieceTargets[i]);
                    go.AddComponent<MeshFilter>();go.AddComponent<MeshRenderer>();
                }
                proxy.gameObject.layer=body.gameObject.layer;
                proxy.transform.SetParent(body.transform.parent,false);
                proxy.transform.SetPositionAndRotation(body.transform.position,body.transform.rotation);
                proxy.transform.localScale=body.transform.localScale;
                proxy.transform.SetParent(carrier.transform,true);
                var rest=_structureRuntime.GetPieceDefinition(i).RestLocalPosition;
                var leaderRest=_structureRuntime.GetPieceDefinition(leader).RestLocalPosition;
                proxy.transform.localPosition=new Vector3(rest.x-leaderRest.x,rest.y-leaderRest.y,rest.z-leaderRest.z);
                proxy.transform.localRotation=Quaternion.identity;proxy.transform.localScale=Vector3.one;
                proxy.sharedMesh=source.sharedMesh;proxy.convex=true;proxy.sharedMaterial=source.sharedMaterial;
                proxy.gameObject.SetActive(true);proxy.enabled=_carrierColliderEnabled[i];
                var renderer=body.GetComponent<MeshRenderer>();
                proxy.GetComponent<MeshFilter>().sharedMesh=body.GetComponent<MeshFilter>().sharedMesh;
                var display=proxy.GetComponent<MeshRenderer>();display.sharedMaterials=renderer.sharedMaterials;display.enabled=renderer.enabled;
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);display.SetPropertyBlock(properties);
                renderer.enabled=false;
                body.isKinematic=true;body.detectCollisions=false;source.enabled=false;

            }
            center=Vector3.zero;
            for(int i=0;i<count;i++)if(_carrierMasses[i]>0)
                center+=(i==leader?carrier.transform:_carrierShapes[i].transform).TransformPoint(_carrierCenters[i])*_carrierMasses[i];
            center/=mass;
            _carrierConstraints=carrier.constraints;carrier.constraints=_rigidSlideCanTip?_carrierConstraints:RigidbodyConstraints.FreezeRotation;
            carrier.mass=mass;carrier.centerOfMass=carrier.transform.InverseTransformPoint(center);carrier.ResetInertiaTensor();
            carrier.linearVelocity=velocity;carrier.angularVelocity=_rigidSlideCanTip?angular:Vector3.zero;
            _rigidCarrier=leader;_rigidCarrierActive=true;
        }
        private bool IsRigidSlideFollower(int index)=>_rigidCarrierActive&&index!=_rigidCarrier&&_carrierMasses[index]>0;
    }
}
