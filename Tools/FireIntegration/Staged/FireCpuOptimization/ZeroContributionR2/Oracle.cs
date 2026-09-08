using System;
using Elemental.Presentation.Fire;
using Elemental.Simulation.Fire;
using Unity.Mathematics;
namespace Elemental.Simulation.Fire { public static class FireWorld { public const int MaximumNodes=6,MaximumContacts=8; } }
internal static class Program
{
    private static uint state=190317;
    private static float R(float low,float high){state^=state<<13;state^=state>>17;state^=state<<5;return low+(high-low)*((state&0xffffff)/16777216f);}
    private static float3 V(float size)=>new float3(R(-size,size),R(-size,size),R(-size,size));
    private static bool Bits(float a,float b)=>BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
    private static bool Bits(float3 a,float3 b)=>Bits(a.x,b.x)&&Bits(a.y,b.y)&&Bits(a.z,b.z);
    private static void Main()
    {
        int sampleCases=0,steerCases=0,nonzeroSampleCases=0,changedSteerCases=0;var snapshot=new FirePresentationSnapshot();
        // Recorded package/domain formulas execute directly, not translated arithmetic.
        // Deterministic finite operating domain includes zero axes, shell supports,
        // inactive nodes, zero density and support boundaries. No IEEE overflow inputs.
        for(int c=0;c<250000;c++)
        {
            snapshot.NodeCount=1+c%6;snapshot.Origin=V(1000);
            for(int i=0;i<snapshot.NodeCount;i++)
            {
                var a=V(20);snapshot.Nodes[i]=new FireFieldNode{A=a,B=c%7==0?a:a+V(8),Up=c%19==0?float3.zero:V(2),Flow=V(20),
                    Radius=R(0.001f,8),ShellHalfThickness=R(0,1.5f),Response=R(0,40),Lift=R(-10,10),Swirl=R(-20,20),NoiseSpeed=R(0,15),NoiseFrequency=R(0,20),
                    Density=c%17==0?0:R(0,1),Phase=R(-20,20),MaxTargetSpeed=R(0,32),Shape=(FireShape)((c+i)%3),Active=c%23!=0};
            }
            var n=snapshot.Nodes[0];float factor=(c%9-4)*0.5f;
            var position=c%2==0?n.A+new float3(n.Radius+n.ShellHalfThickness*factor,0,0):V(50);float time=R(-10000,10000);
            FireCpuFieldReference.Sample(snapshot,position,time,out var before,out float responseBefore);
            FireCpuFieldCandidate.Sample(snapshot,position,time,out var after,out float responseAfter);
            if(!math.all(math.isfinite(before)) || !float.IsFinite(responseBefore))throw new Exception("Nonfinite oracle baseline");
            if(!Bits(before,after)||!Bits(responseBefore,responseAfter))throw new Exception("Sample mismatch case "+c+" before "+before+" after "+after);
            if(math.any(before!=float3.zero)||responseBefore!=0)nonzeroSampleCases++;
            sampleCases++;
        }
        for(int c=0;c<250000;c++)
        {
            var normal=math.normalizesafe(V(1),new float3(0,1,0));var tangent=FireContactMath.Tangent(normal);
            var patch=new FireContactPatch{Point=V(100),Normal=c%19==0?float3.zero:normal,Tangent=c%7==0?normal:V(2),Radius=R(.01f,10),
                FrontDepth=R(0,4),RecoveryDepth=R(0,1),SurfaceVelocity=V(15),AngularVelocity=V(2),ResponseRate=R(0,40),SpreadFraction=R(0,1),Skin=R(0,.1f),Active=c%23!=0};
            float radius=R(0,.3f),localTime=R(0,1),phase=R(-10000,10000),dt=c%29==0?0:R(0,.05f);
            float distance=c%11==0?-patch.RecoveryDepth:c%11==1?patch.FrontDepth:R(-1,5);
            float lateral=c%13==0?0:c%13==1?patch.Radius:R(0,patch.Radius*1.1f);
            float3 q=normal*(radius+patch.Skin+distance)+tangent*lateral;
            var position=patch.Point+patch.SurfaceVelocity*localTime+q;
            var velocity=patch.SurfaceVelocity+math.cross(patch.AngularVelocity,q)+V(12)+normal*(c%2==0?20:-20);
            var before=velocity;var after=velocity;
            FireCpuFieldReference.Steer(patch,position,localTime,radius,phase,dt,ref before);
            FireCpuFieldCandidate.Steer(patch,position,localTime,radius,phase,dt,ref after);
            if(!math.all(math.isfinite(before)))throw new Exception("Nonfinite steering baseline");
            if(!Bits(before,after))throw new Exception("Steer mismatch case "+c+" before "+before+" after "+after);
            if(!Bits(before,velocity))changedSteerCases++;
            steerCases++;
        }
        if(nonzeroSampleCases<10000||changedSteerCases<10000)throw new Exception("Insufficient active calculation coverage");
        Console.WriteLine("{\"sampleCases\":"+sampleCases+",\"steerCases\":"+steerCases+",\"nonzeroSampleCases\":"+nonzeroSampleCases+",\"changedSteerCases\":"+changedSteerCases+",\"bitwiseEqual\":true,\"finiteOperatingDomain\":true,\"burstExecuted\":false,\"performanceMeasured\":false}");
    }
}
