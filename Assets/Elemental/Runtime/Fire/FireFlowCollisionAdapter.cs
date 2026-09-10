using Elemental.Simulation.Fire;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Elemental.Runtime.Fire
{
    // Every transported parcel probes current PhysX geometry, including moving stones.
    public sealed class FireFlowCollisionAdapter : IFireFlowCollision, System.IDisposable
    {
        private static readonly ProfilerMarker Marker=new ProfilerMarker("Fire.Flow.Collision");
        private readonly RaycastHit[] hits=new RaycastHit[24];
        private readonly Collider[] overlaps=new Collider[24];
        private readonly int mask;
        private readonly GameObject queryObject;
        private readonly SphereCollider querySphere;
        private Transform ignoredRoot;
        public int Saturations {get;private set;}
        public Collider LastBlockedCollider {get;private set;}
        public string LastBlockReason {get;private set;}
        public float3 LastBlockedPosition {get;private set;}
        public float LastBlockedRadius {get;private set;}
        public int MeshRecoveryRays {get;private set;}
        public int RecoveredMeshOverlaps {get;private set;}
        public int UnresolvedMeshOverlaps {get;private set;}
        public int DeepMeshOverlaps {get;private set;}
        public int InitialCastSentinels {get;private set;}
        private void RecordBlock(string reason,float3 position,float radius,Collider collider)
        {LastBlockedCollider=collider;LastBlockReason=reason;LastBlockedPosition=position;LastBlockedRadius=radius;}
        public FireFlowCollisionAdapter(int collisionMask,Transform emitterRoot=null)
        {
            mask=collisionMask;ignoredRoot=emitterRoot;
            queryObject=new GameObject("Fire flow disabled overlap probe"){hideFlags=HideFlags.HideAndDontSave};
            querySphere=queryObject.AddComponent<SphereCollider>();querySphere.enabled=false;
        }
        public void Dispose(){if(queryObject!=null)Object.Destroy(queryObject);}
        public void SetEmitter(Transform root)=>ignoredRoot=root;
        private bool Ignore(Collider c)=>c==null || (ignoredRoot!=null && c.transform.IsChildOf(ignoredRoot));
        public bool Sweep(float3 position,float radius,float3 displacement,out FireFlowHit result)
        {
            using var marker=Marker.Auto();
            result=default;
            Vector3 origin=position,move=displacement;
            float distance=move.magnitude;
            if(distance<.000001f)return false;
            // SphereCast alone misses initial overlaps (birth beside a wall or moving geometry).
            // Match the solver's4mm clearance. Exact-radius overlap/MTD can miss a
            // submillimetre contact while SphereCast reports its initial-overlap
            // sentinel (distance0, point0, normal opposite travel).
            const float contactSkin=.004f;
            float probeRadius=radius+contactSkin;
            int count=UnityEngine.Physics.OverlapSphereNonAlloc(origin,probeRadius,overlaps,mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length){Saturations++;result=new FireFlowHit{Blocked=true,Normal=-math.normalizesafe(displacement),Point=position};return true;}
            int recoveryRaysRemaining=6;
            float deepest=0;Collider overlap=null;Vector3 surface=default,normal=default;
            for(int i=0;i<count;i++)
            {
                Collider c=overlaps[i];if(Ignore(c))continue;
                Vector3 away; float depth; Vector3 contactPoint;
                // Primitive and convex closest-point queries return the real surface even
                // for submillimetre overlaps where the disabled probe's MTD can fail.
                bool closestSupported=c is BoxCollider || c is SphereCollider || c is CapsuleCollider
                    || (c is MeshCollider mesh && mesh.convex);
                if(closestSupported)
                {
                    contactPoint=c.ClosestPoint(origin);
                    Vector3 separation=origin-contactPoint;
                    float separationLength=separation.magnitude;
                    if(separationLength<.000001f)
                    { // ClosestPoint returns the input when the center is inside solid.
                        RecordBlock("inside-solid",position,radius,c);
                        result=new FireFlowHit{Blocked=true,Point=position};return true;
                    }
                    if(separationLength>probeRadius)continue;
                    away=separation/separationLength;depth=probeRadius-separationLength;
                }
                else
                {
                    querySphere.radius=probeRadius;
                    if(!UnityEngine.Physics.ComputePenetration(querySphere,origin,Quaternion.identity,c,c.transform.position,c.transform.rotation,out away,out depth))
                    {
                        // Nonconvex triangle MTD can fail for shallow radius-growth overlap.
                        // Recover only from actual front-facing triangle hits, never guessed
                        // normals or an untested move through SphereCast's initial sentinel.
                        if(!TryRecoverMeshContact(c,origin,probeRadius,ref recoveryRaysRemaining,out away,out depth,out contactPoint))
                        {
                            UnresolvedMeshOverlaps++;RecordBlock("unresolved-mesh-overlap",position,radius,c);
                            result=new FireFlowHit{Blocked=true,Point=position};return true;
                        }
                        RecoveredMeshOverlaps++;
                    }
                    else contactPoint=origin+away*(depth-probeRadius);
                    if(depth>=probeRadius)
                    {
                        DeepMeshOverlaps++;RecordBlock("deep-mesh-overlap",position,radius,c);
                        result=new FireFlowHit{Blocked=true,Point=position};return true;
                    }
                }
                // The 4mm shell is broadphase robustness, NOT solid gas radius.
                // A corrected parcel inside that shell must be able to travel tangentially
                // or away; forward motion is still tested by the real-radius SphereCast.
                // Retain 10 micrometres of numerical tolerance at actual physical touch.
                if(depth<contactSkin-.00001f)continue;
                if(depth>deepest){deepest=depth;overlap=c;normal=away;surface=contactPoint;}

            }
            if(overlap!=null)
            {result=new FireFlowHit{Fraction=0,Point=surface,Normal=normal,SurfaceVelocity=Velocity(overlap,surface)};return true;}
            count=UnityEngine.Physics.SphereCastNonAlloc(origin,radius,move/distance,hits,distance+.002f,mask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length){Saturations++;result=new FireFlowHit{Blocked=true,Normal=-math.normalizesafe(displacement),Point=position};return true;}
            float nearest=float.PositiveInfinity;int selected=-1;
            for(int i=0;i<count;i++)if(!Ignore(hits[i].collider)&&hits[i].distance<nearest){selected=i;nearest=hits[i].distance;}
            if(selected<0)return false;
            var hit=hits[selected];
            // PhysX documents distance=0, point=0 for an initially overlapping sphere.
            // Such a result is not a world-space contact and must never move a parcel.
            if(hit.distance<=0 && hit.point==Vector3.zero)
            {InitialCastSentinels++;RecordBlock("initial-cast-sentinel",position,radius,hit.collider);result=new FireFlowHit{Blocked=true,Point=position};return true;}
            result=new FireFlowHit{Fraction=math.saturate(hit.distance/distance),Point=hit.point,Normal=hit.normal,SurfaceVelocity=Velocity(hit.collider,hit.point)};
            return true;
        }
        private bool TryRecoverMeshContact(Collider collider,Vector3 origin,float radius,ref int remaining,
            out Vector3 normal,out float depth,out Vector3 point)
        {
            normal=point=default;depth=0;bool found=false;
            const float rayTolerance=.001f;
            // Six world-axis rays are independent of emitter aim and gravity. Their hits
            // retain the collider's triangle normal, including sloped terrain and corners.
            // A missed edge/inside-solid stays unresolved and is deliberately rejected.
            for(int axis=0;axis<6&&remaining>0;axis++)
            {
                remaining--;MeshRecoveryRays++;
                Vector3 direction=axis==0?Vector3.up:axis==1?Vector3.down:axis==2?Vector3.right:axis==3?Vector3.left:axis==4?Vector3.forward:Vector3.back;
                if(!collider.Raycast(new Ray(origin,direction),out RaycastHit hit,radius+rayTolerance))continue;
                if(Vector3.Dot(hit.normal,-direction)<.0001f)continue;
                float separation=Vector3.Dot(origin-hit.point,hit.normal);
                if(separation<=.00001f||separation>radius+rayTolerance||hit.distance>radius+rayTolerance||hit.normal.sqrMagnitude<.99f)continue;
                float penetration=Mathf.Max(0,radius-separation);
                if(!found||penetration>depth){found=true;depth=penetration;normal=hit.normal;point=hit.point;}
            }
            return found;
        }
        // Three bounded downward probes per active stream place its existing light pool along terrain.
        public bool TryGroundLight(float3 point,float3 up,out Vector3 position)
        {
            Vector3 direction=-(Vector3)math.normalizesafe(up,new float3(0,1,0));
            int count=UnityEngine.Physics.RaycastNonAlloc((Vector3)point-direction*.1f,direction,hits,8,mask,QueryTriggerInteraction.Ignore);
            position=point;if(count==hits.Length){Saturations++;return false;}
            float nearest=float.PositiveInfinity;int selected=-1;
            for(int i=0;i<count;i++)if(!Ignore(hits[i].collider)&&Vector3.Dot(hits[i].normal,-direction)>.3f&&hits[i].distance<nearest){selected=i;nearest=hits[i].distance;}
            if(selected<0)return false;
            position=hits[selected].point-direction*.7f;return true;
        }
        private static Vector3 Velocity(Collider collider,Vector3 point)=>collider.attachedRigidbody==null?Vector3.zero:collider.attachedRigidbody.GetPointVelocity(point);
    }
}
