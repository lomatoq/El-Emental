using System;
using System.Text;
using System.IO;
using Unity.Mathematics;
using Elemental.Presentation.DistantScenery;
class Oracle
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static string Signature(RockShapeData s){var b=new StringBuilder();foreach(var p in s.Parts){b.Append(p.Translation).Append(p.Scale).Append(p.Rotation);foreach(var f in p.Solid.Faces)foreach(var q in f.Points)b.Append(q);}return b.ToString();}
 static string Signature(ReferenceData s){var b=new StringBuilder();foreach(var p in s.Parts){b.Append(p.Translation).Append(p.Scale).Append(p.Rotation);foreach(var f in p.Solid.Faces)foreach(var q in f.Points)b.Append(q);}return b.ToString();}
 static void Main()
 {
  int count=0, existingGroundRejects=0;float minAspect=9,maxAspect=0,minHeight=9,maxHeight=0;
  for(int sample=0;sample<48;sample++)for(int family=0;family<6;family++)
  {
   int seed=13771+sample*1013+family*131;var high=RockShapeBuilder.FloatingPillar(seed,family,false,RockShapeSettings.Default);var low=RockShapeBuilder.FloatingPillar(seed,family,true,RockShapeSettings.Default);
   foreach(var s in new[]{high,low})
   {
    Check(s.Validate(out _),"closed "+seed);s.Bounds(out var min,out var max);float height=max.y-min.y,aspect=(max.x-min.x)/height;
    Check(height>=.4f&&height<=.5f,"height "+height);Check(aspect>=.25f&&aspect<=.65f,"aspect "+aspect);Check(s.Parts[0].Scale.y>height*.74f,"core");
    minHeight=math.min(minHeight,height);maxHeight=math.max(maxHeight,height);minAspect=math.min(minAspect,aspect);maxAspect=math.max(maxAspect,aspect);
    for(int i=1;i<3;i++){var end=s.Parts[i];var core=s.Parts[0];Check(end.Scale.x/core.Scale.x>=.74f&&end.Scale.x/core.Scale.x<=.90f,"caps");Check(math.abs(end.Translation.x-core.Translation.x)<core.Scale.x*.12f,"offset");}
   }
   for(int i=0;i<3;i++){Check(high.Parts[i].Translation.Equals(low.Parts[i].Translation)&&high.Parts[i].Scale.Equals(low.Parts[i].Scale)&&high.Parts[i].Rotation.Equals(low.Parts[i].Rotation),"LOD transform");}
   high.Bounds(out var hmin,out var hmax);low.Bounds(out var lmin,out var lmax);Check(math.all(hmin>=lmin-.0001f)&&math.all(hmax<=lmax+.0001f),"LOD envelope");
   string gs,rs;try{gs=Signature(RockShapeBuilder.Group(seed,5,false,family,true,RockShapeSettings.Default));}catch(InvalidOperationException e){gs="REJECT:"+e.Message;existingGroundRejects++;}try{rs=Signature(ReferenceBuilder.Group(seed,5,false,family,true,RockShapeSettings.Default));}catch(InvalidOperationException e){rs="REJECT:"+e.Message;}Check(gs==rs,"Ground changed");count++;
  }
  Console.WriteLine("Existing matching ground seed rejections="+existingGroundRejects);Console.WriteLine("PASS "+count+" seeds/families, both LODs closed; exact coarse transforms; ground signatures unchanged. height="+minHeight+".."+maxHeight+" width/height="+minAspect+".."+maxAspect);
 }
}
