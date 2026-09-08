using System;
using Elemental.Simulation.Fire;
using Unity.Mathematics;

namespace Elemental.Presentation.Fire
{
    // Cosmetic CPU mirror of FireFieldCommon.hlsl. It never writes FireWorld state.
    internal static unsafe class FireCpuField
    {
        public static void Sample(FirePresentationSnapshot snapshot, float3 position, float time, out float3 target, out float response)
        {
            fixed(FireFieldNode* nodes=snapshot.Nodes) Sample(nodes,snapshot.NodeCount,snapshot.Origin,position,time,out target,out response);
        }
        public static void Sample(FireFieldNode* nodes,int nodeCount,float3 origin,float3 position,float time,out float3 target,out float response)
        {
            target=0; response=0; float total=0;
            for(int i=0;i<nodeCount;i++)
            {
                var node=nodes[i]; if(!node.Active || node.Density<=0) continue;
                float radius=math.max(node.Radius,0.001f);
                float3 up=FireContactMath.SafeNormal(node.Up,new float3(0,1,0));
                float3 axis=FireContactMath.SafeNormal(node.B-node.A,up);
                float3 ab=node.B-node.A;
                float3 centre=node.A+ab*math.saturate(math.dot(position-node.A,ab)/math.max(math.lengthsq(ab),1e-8f));
                float3 radial=position-centre; float distance=math.length(radial);
                float weight=1-math.smoothstep(radius*0.6f,radius,distance);
                float3 velocity=node.Flow+up*node.Lift;
                if(node.Shape==FireShape.Shell)
                {
                    radial=position-node.A; distance=math.length(radial); float3 normal=FireContactMath.SafeNormal(radial,up);
                    float thickness=math.max(node.ShellHalfThickness,0.001f);
                    weight=1-math.smoothstep(thickness*0.6f,thickness,math.abs(distance-radius));
                    velocity-=normal*math.dot(velocity,normal);
                    velocity-=normal*(distance-radius)*math.max(node.Response,0);
                }
                velocity+=math.cross(axis,radial)*node.Swirl;
                // GPU field coordinates are origin-relative; preserve the same phase on CPU.
                float3 q=(position-origin)*math.max(node.NoiseFrequency,0.001f)+node.Phase;
                float3 noise=0.5f*new float3(math.sin(q.y+time)+math.cos(q.z-0.7f*time),
                    math.sin(q.z+0.6f*time)+math.cos(q.x+time),math.sin(q.x-0.8f*time)+math.cos(q.y+0.4f*time));
                velocity+=noise*node.NoiseSpeed;
                velocity=FireContactMath.Limit(velocity,math.max(node.MaxTargetSpeed,0));
                weight*=math.saturate(node.Density); target+=velocity*weight; response+=math.max(node.Response,0)*weight; total+=weight;
            }
            if(total>1e-6f) { target/=total; response=response/total*math.saturate(total); }
        }
        public static void Steer(in FireContactPatch patch,float3 position,float localTime,float radius,float phase,float dt,ref float3 velocity)
        {
            if(!patch.Active) return;
            float3 normal=FireContactMath.SafeNormal(patch.Normal,new float3(0,1,0));
            float3 q=position-(patch.Point+patch.SurfaceVelocity*localTime);
            float signedDistance=math.dot(q,normal);
            float distance=signedDistance-math.max(radius+patch.Skin,0);
            if(distance < -math.max(patch.RecoveryDepth,0) || distance>math.max(patch.FrontDepth,0.001f)) return;
            float3 lateral=q-normal*signedDistance; float r=math.length(lateral); if(r>=patch.Radius) return;
            float weight=(1-math.smoothstep(patch.Radius*0.75f,patch.Radius,r)) *
                (1-math.smoothstep(0,math.max(patch.FrontDepth,0.001f),math.max(distance,0)));
            float3 tangent=FireContactMath.SafeNormal(patch.Tangent-normal*math.dot(patch.Tangent,normal),FireContactMath.Tangent(normal));
            float3 fallback=tangent*math.cos(phase)+math.cross(normal,tangent)*math.sin(phase);
            float3 outward=FireContactMath.SafeNormal(lateral,fallback);
            float3 surface=patch.SurfaceVelocity+math.cross(patch.AngularVelocity,q);
            float3 relative=velocity-surface; if(math.dot(relative,normal)>=0) return;
            float3 redirected=FireContactMath.RedirectRelative(relative,normal,outward,patch.SpreadFraction);
            float blend=1-math.exp(-math.max(patch.ResponseRate,0)*math.max(dt,0)*weight);
            velocity=surface+math.lerp(relative,redirected,blend);
        }
    }
}

