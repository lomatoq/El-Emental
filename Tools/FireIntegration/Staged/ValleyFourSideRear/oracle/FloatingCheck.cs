using System;
using System.IO;
using System.Text.Json;
using Unity.Mathematics;
using Elemental.Presentation.DistantScenery;
class FloatingCheck
{
    static int Main()
    {
        try
        {
            float minHeight=10,maxHeight=0;int cases=0;
            for(int family=0;family<6;family++)for(int sample=0;sample<8;sample++)foreach(bool low in new[]{false,true})
            {
                int seed=13771+sample*1013+family*131;
                var original=RockShapeBuilder.Pillar(seed,true,RockShapeSettings.Default);
                var shape=RockShapeBuilder.FloatingPillar(seed,family,low,RockShapeSettings.Default);
                if(!shape.Validate(out _)||shape.Parts.Count!=original.Parts.Count)throw new Exception("Solid contract");
                original.Bounds(out var omin,out var omax);shape.Bounds(out var min,out var max);
                minHeight=math.min(minHeight,max.y-min.y);maxHeight=math.max(maxHeight,max.y-min.y);
                for(int i=0;i<shape.Parts.Count;i++)
                    if(math.any(math.abs(shape.Parts[i].Scale-original.Parts[i].Scale/(omax.y-omin.y))>1e-5f))throw new Exception("Post-mesh squash");
                if(max.y-min.y<.4f||max.y-min.y>.5f)throw new Exception("Height envelope");
                cases++;
            }
            var report=new{cases,minHeight,maxHeight,status="PASS"};
            File.WriteAllText("floating-report.json",JsonSerializer.Serialize(report));Console.WriteLine(JsonSerializer.Serialize(report));return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
