using UnityEngine;
namespace Elemental.Runtime.Fire
{
    /// <summary>Bounded surface queries. Concave meshes never use unsupported ClosestPoint or fabricate a center contact.</summary>
    public static class FireColliderSurface
    {
        public static bool SupportsClosestPoint(Collider c)=>c is BoxCollider||c is SphereCollider||c is CapsuleCollider||(c is MeshCollider mesh&&mesh.convex);
        public static bool TryPoint(Collider c,Vector3 origin,Vector3 preferred,float range,out Vector3 point)
        {
            point=default;if(c==null||!c.enabled||!Finite(origin)||!float.IsFinite(range)||range<0)return false;
            if(SupportsClosestPoint(c)){point=c.ClosestPoint(origin);return (point-origin).sqrMagnitude<=range*range;}
            bool found=false;float nearest=range;
            if(preferred.sqrMagnitude>.00001f&&Finite(preferred)&&c.Raycast(new Ray(origin,preferred.normalized),out var hit,nearest))
            {point=hit.point;nearest=hit.distance;found=true;}
            Vector3 toward=c.bounds.center-origin;
            if(toward.sqrMagnitude>.00001f&&c.Raycast(new Ray(origin,toward.normalized),out hit,nearest))
            {point=hit.point;found=true;}
            return found;
        }
        public static bool ContainsSurfacePoint(Collider c,Vector3 point,Vector3 normal,float tolerance=.2f)
        {
            if(c==null||!Finite(point)||!Finite(normal)||normal.sqrMagnitude<.00001f)return false;
            if(SupportsClosestPoint(c))return (c.ClosestPoint(point)-point).sqrMagnitude<=tolerance*tolerance;
            // Real contact normals normally resolve on the first pair. Axis pairs handle tangential area-query normals.
            if(NearRay(c,point,normal.normalized,tolerance))return true;
            if(NearRay(c,point,Vector3.up,tolerance))return true;
            if(NearRay(c,point,Vector3.right,tolerance))return true;
            return NearRay(c,point,Vector3.forward,tolerance);
        }
        static bool NearRay(Collider c,Vector3 point,Vector3 axis,float tolerance)
        {
            float reach=tolerance+.02f;
            return (c.Raycast(new Ray(point+axis*reach,-axis),out var hit,reach*2)&&(hit.point-point).sqrMagnitude<=tolerance*tolerance)||
                   (c.Raycast(new Ray(point-axis*reach,axis),out hit,reach*2)&&(hit.point-point).sqrMagnitude<=tolerance*tolerance);
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    }
}
