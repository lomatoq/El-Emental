using UnityEngine;
namespace Elemental.Runtime.Physics
{
    public sealed partial class EarthWall
    {
        private Mesh[] _foundationOriginalMeshes,_foundationContactMeshes;
        private bool[] _foundationOriginalEnabled;
        private void PrepareDomainFoundationContact(int index)
        {
            if(_pieceColliders[index] is not MeshCollider collider||collider.sharedMesh==null)return;
            if(_foundationOriginalMeshes==null||_foundationOriginalMeshes.Length!=_pieceColliders.Length)
            {
                _foundationOriginalMeshes=new Mesh[_pieceColliders.Length];
                _foundationContactMeshes=new Mesh[_pieceColliders.Length];
                _foundationOriginalEnabled=new bool[_pieceColliders.Length];
            }
            var source=collider.sharedMesh;
            var vertices=source.vertices;
            Vector3 plane=transform.TransformPoint(_collider.center-Vector3.up*(_collider.size.y*.5f));
            float highest=float.NegativeInfinity;bool trimmed=false;
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 point=collider.transform.TransformPoint(vertices[i]);
                float height=Vector3.Dot(point-plane,_up);highest=Mathf.Max(highest,height);
                if(height>=0)continue;
                vertices[i]=collider.transform.InverseTransformPoint(point-_up*height);trimmed=true;
            }
            if(!trimmed)return;
            _foundationOriginalMeshes[index]=source;_foundationOriginalEnabled[index]=collider.enabled;
            // Entirely buried foundation cells retain canonical mass/bonds but have no exposed contact hull.
            if(highest<.01f){collider.enabled=false;return;}
            Vector3 massCenter=_pieceBodies[index].centerOfMass;
            var mesh=Instantiate(source);mesh.name=source.name+" exposed foundation contact";
            mesh.vertices=vertices;mesh.RecalculateBounds();
            _foundationContactMeshes[index]=mesh;collider.sharedMesh=mesh;
            _pieceBodies[index].centerOfMass=massCenter;
        }
        private void RestoreDomainFoundationContact()
        {
            if(_foundationOriginalMeshes==null)return;
            for(int i=0;i<_foundationOriginalMeshes.Length;i++)
            {
                if(_foundationOriginalMeshes[i]!=null&&_pieceColliders!=null&&i<_pieceColliders.Length&&_pieceColliders[i] is MeshCollider collider)
                {
                    collider.sharedMesh=_foundationOriginalMeshes[i];collider.enabled=_foundationOriginalEnabled[i];
                    if(_pieceBodies[i]!=null)_pieceBodies[i].ResetCenterOfMass();
                }
                if(_foundationContactMeshes[i]!=null)Destroy(_foundationContactMeshes[i]);
            }
            _foundationOriginalMeshes=null;_foundationContactMeshes=null;_foundationOriginalEnabled=null;
        }
        private void OnDestroy()=>RestoreDomainFoundationContact();
    }
}