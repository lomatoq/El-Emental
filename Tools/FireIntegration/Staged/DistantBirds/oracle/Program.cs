using System;
using System.IO;
using System.Text.Json;
using Unity.Mathematics;
using Elemental.Presentation.DistantScenery;
class Program
{
    static int Main()
    {
        try
        {
            float maxStep=0,maxDistance=0,sum=0;int cases=0;
            for(int index=0;index<24;index++)
            {
                var bird=DistantBirdFlight.Create(38217,index);
                for(int sample=0;sample<360;sample++)
                {
                    double time=sample*bird.Period/20;
                    var pose=DistantBirdFlight.Evaluate(bird,time,false);var near=DistantBirdFlight.Evaluate(bird,time+.001,false);
                    float distance=math.distance(pose.Position,bird.Center),step=math.distance(pose.Position,near.Position);
                    if(!math.all(math.isfinite(pose.Position))||distance>=130||step>=.02)throw new Exception("Flight continuity/bounds");
                    maxDistance=math.max(maxDistance,distance);maxStep=math.max(maxStep,step);cases++;
                }
            }
            var one=DistantBirdFlight.Create(38217,0);for(int i=0;i<100;i++)sum+=DistantBirdFlight.Evaluate(one,i,false).Position.x;
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<100000;i++)sum+=DistantBirdFlight.Evaluate(one,i*.001,false).Position.x;
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            if(allocated!=0||float.IsNaN(sum))throw new Exception("Allocation contract");
            var result=new{cases,maxStep,maxDistance,evaluations=100000,managedBytes=allocated,status="PASS"};
            Console.WriteLine(JsonSerializer.Serialize(result));File.WriteAllText("report.json",JsonSerializer.Serialize(result));return 0;
        }
        catch(Exception error){Console.WriteLine(error);return 1;}
    }
}
