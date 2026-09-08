using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Elemental.Presentation.DistantScenery
{
    [Serializable] public struct RockShapeSettings
    {
        public float asymmetry,bevelInset,chipDepth;
        public int chips;
        public static RockShapeSettings Default=>new RockShapeSettings{asymmetry=.18f,bevelInset=.04f,chipDepth=.10f,chips=3};
    }
    public sealed class RockShapeData
    {
        public sealed class Part
        {
            public RockPolyhedron Solid;
            public float3 Translation,Scale;
            public quaternion Rotation;
            public float Tone;
            public float3 Point(float3 p)=>Translation+math.rotate(Rotation,p*Scale);
        }
        public readonly List<Part> Parts=new List<Part>();
        public int RejectedCuts;
        public int TriangleCount
        {get{int count=0;foreach(var part in Parts)foreach(var face in part.Solid.Faces)count+=face.Points.Count-2;return count;}}
        public bool Validate(out double volume)
        {volume=0;foreach(var part in Parts){if(!part.Solid.Validate(out double v) || math.any(part.Scale<=0))return false;volume+=v*part.Scale.x*part.Scale.y*part.Scale.z;}return Parts.Count>0 && volume>0;}
        public void Bounds(out float3 min,out float3 max)
        {min=new float3(float.PositiveInfinity);max=new float3(float.NegativeInfinity);foreach(var part in Parts)foreach(var face in part.Solid.Faces)foreach(float3 p in face.Points){float3 q=part.Point(p);min=math.min(min,q);max=math.max(max,q);}}
        public void NormalizeHeight()
        {Bounds(out float3 min,out float3 max);float h=max.y-min.y;float3 center=new float3((min.x+max.x)*.5f,min.y,(min.z+max.z)*.5f);foreach(var part in Parts){part.Translation=(part.Translation-center)/h;part.Scale/=h;}}
    }
    /// <summary>One shape seed describes both LODs. LOD removes small bevels only;
    /// major planes, compound offsets and chips stay identical.</summary>
    public static class RockShapeBuilder
    {
        public static RockShapeData Pillar(int seed,bool lowDetail,RockShapeSettings settings)
            =>BuildPillar(seed,lowDetail,settings,1f);
        private static RockShapeData BuildPillar(int seed,bool lowDetail,RockShapeSettings settings,float heightRatio)
        {
            var result=new RockShapeData();var random=new RockRandom(seed);
            int pieces=random.Count(2,4);float height=random.Next(2.7f,4.5f),width=random.Next(.9f,1.2f);
            float lean=random.Next(-.10f,.10f);
            // One coherent stone tower formed by unequal overlapping vertical masses.
            // Broad crosswise shoulders interrupt the long extrusion; no equal rings.
            float[] weights=new float[pieces];float sum=0;
            for(int i=0;i<pieces;i++){weights[i]=random.Next(.65f,1.4f);sum+=weights[i];}
            float y=0,previousHeight=0;float drift=random.Next(-.10f,.10f);
            for(int i=0;i<pieces;i++)
            {
                float h=height*weights[i]/sum*1.22f;
                float w=width*(i==0?random.Next(.96f,1.18f):random.Next(.76f,1.12f));
                if(i>0)y+=previousHeight-Math.Min(previousHeight,h)*.20f;
                float x=drift+i*lean*.22f+(i%2==0?-.13f:.16f)*width;
                float z=random.Next(-.12f,.12f)*width;
                Add(result,unchecked(seed+i*73856093),new float3(w,h,width*random.Next(.72f,1.03f)),new float3(x,y*heightRatio,z),lean,lowDetail,settings,heightRatio);
                previousHeight=h;
            }
            return result;
        }
        public static RockShapeData FloatingPillar(int seed,int family,bool lowDetail,RockShapeSettings settings)
        {
            // The middle is a continuous full-width body. Caps overlap this core;
            // they never alternate wide shelves around a narrow, open waist.
            float targetRatio=math.lerp(.415f,.485f,math.clamp(family,0,5)/5f);
            var result=FloatingBody(seed,lowDetail,settings);
            var coarse=lowDetail?result:FloatingBody(seed,true,settings);
            coarse.Bounds(out float3 min,out float3 max);
            float scale=targetRatio/(max.y-min.y);
            float3 origin=new float3((min.x+max.x)*.5f,min.y,(min.z+max.z)*.5f);
            foreach(var part in result.Parts){part.Translation=(part.Translation-origin)*scale;part.Scale*=scale;}
            return result;
        }
        private static RockShapeData FloatingBody(int seed,bool low,RockShapeSettings settings)
        {
            var result=new RockShapeData();var random=new RockRandom(seed);
            float width=random.Next(.52f,.60f),depth=width*random.Next(.76f,.94f);
            float lean=random.Next(-.025f,.025f);
            // Full-height core prevents a C-shaped hole from every viewing angle.
            Add(result,seed,new float3(width,.91f,depth),new float3(0,.045f,0),lean,low,settings);
            float lowerWidth=width*random.Next(.77f,.89f),upperWidth=width*random.Next(.75f,.88f);
            Add(result,unchecked(seed+73856093),new float3(lowerWidth,random.Next(.31f,.39f),depth*.87f),
                new float3(random.Next(-.085f,.085f)*width,0,depth*.06f),-lean*.6f,low,settings);
            Add(result,unchecked(seed+147712186),new float3(upperWidth,random.Next(.32f,.42f),depth*.84f),
                new float3(random.Next(-.10f,.10f)*width,.66f,random.Next(-.06f,.06f)*depth),lean*.7f,low,settings);
            // Clip world-space oblique facets into both ends of the compound.
            // Plane clipping retains planar faces and closed caps; the middle stays broad.
            for(int i=0;i<8;i++)
            {
                float a=i*math.PI*.25f;
                float3 radial=new float3(math.cos(a)/width,0,math.sin(a)/depth);
                TaperEnd(result,radial+new float3(0,.5f,0),.825f);
                TaperEnd(result,radial-new float3(0,.5f,0),.325f);
            }
            return result;
        }
        private static void TaperEnd(RockShapeData shape,float3 normal,float distance)
        {
            foreach(var part in shape.Parts)
            {
                float3 localNormal=math.rotate(math.inverse(part.Rotation),normal)*part.Scale;
                float localDistance=distance-math.dot(normal,part.Translation);
                // Redundant planes leave the original closed solid unchanged.
                bool outside=false;
                foreach(var face in part.Solid.Faces)foreach(var point in face.Points)
                    if(math.dot(localNormal,point)>localDistance+part.Solid.Epsilon*8)outside=true;
                if(!outside)continue;
                if(!part.Solid.Clip(localNormal,localDistance,out var clipped)||!clipped.Validate(out _))
                    throw new InvalidOperationException("Floating end facet failed closed-solid validation.");
                part.Solid=clipped;
            }
        }
        public static RockShapeData Group(int seed,int pillars,bool floating,int family,bool lowDetail,RockShapeSettings settings)
        {
            // User's latest reference requests floating versions of these exact
            // compound pillars, not a slab supporting a miniature group of towers.
            if(floating)return FloatingPillar(seed,family,lowDetail,settings);
            var result=new RockShapeData();var random=new RockRandom(seed);
            pillars=math.clamp(pillars,floating?2:3,floating?5:7);
            float stretch=floating && family%3==0?.72f:1f;
            float dominant=random.Next(3.4f,4.9f),lean=random.Next(-.07f,.07f);
            for(int i=0;i<pillars;i++)
            {
                float x=(i-(pillars-1)*.5f)*.65f*stretch;
                float envelope=1f-.33f*math.abs(i-(pillars-1)*.43f)/math.max(1,pillars*.5f);
                float h=dominant*envelope*(1+.12f*math.sin(i*1.6f+seed*.01f));
                if(floating && family%3==0)h*=1.22f;
                if(floating && family%3==1)h*=.72f;
                var pillar=Pillar(unchecked(seed+i*92821),lowDetail,settings);
                RockShapeData descriptor=lowDetail?pillar:Pillar(unchecked(seed+i*92821),true,settings);
                descriptor.Bounds(out float3 min,out float3 max);
                float3 size=max-min;float scale=h/size.y;
                foreach(var part in pillar.Parts)
                {
                    part.Translation=(part.Translation-new float3((min.x+max.x)*.5f,min.y,(min.z+max.z)*.5f))*scale+
                        new float3(x,.28f,math.sin(i*.9f+seed)*.22f);
                    part.Scale*=scale;
                    result.Parts.Add(part);
                }
                result.RejectedCuts+=pillar.RejectedCuts;
            }
            // Wide, closed asymmetrical slab connects every column. Floating families
            // vary depth, yaw and bottom cut; none uses an inverted cone primitive.
            float baseDepth=floating?random.Next(.65f,1.55f):.62f;
            Add(result,unchecked(seed^0x33abc21),new float3(pillars*.73f*stretch,baseDepth,1.8f),
                new float3(0,-baseDepth*.72f,0),lean,lowDetail,settings);
            float fixedHeight=dominant*(floating && family%3==0?1.22f:1f)+1f;
            foreach(var part in result.Parts){part.Translation=(part.Translation+new float3(0,baseDepth,0))/fixedHeight;part.Scale/=fixedHeight;}
            return result;
        }
        private static void Add(RockShapeData output,int seed,float3 scale,float3 translation,float lean,bool low,RockShapeSettings settings,float heightRatio=1f)
        {
            var random=new RockRandom(seed);int sides=random.Count(5,8);
            float asymmetry=math.clamp(settings.asymmetry,.02f,.3f);
            var body=RockPolyhedron.Box(new float3(-1,-.3f*heightRatio,-1),new float3(1,1.3f*heightRatio,1));
            for(int i=0;i<sides;i++)
            {
                float a=(i+random.Next(-.16f,.16f))*math.PI*2/sides;
                float3 normal=new float3(math.cos(a),random.Next(.015f,.05f),math.sin(a));
                Required(ref body,normal,.5f*random.Next(1-asymmetry,1+asymmetry));
            }
            Required(ref body,new float3(random.Next(-.13f,.13f),1,random.Next(-.11f,.11f)),heightRatio);
            Required(ref body,new float3(random.Next(-.04f,.04f),-1,random.Next(-.05f,.05f)),0);
            // All LODs receive exactly the same coarse corner cuts before detail.
            int chips=math.clamp(settings.chips,2,5);
            for(int chip=0;chip<chips;chip++)
            {
                var top=new List<float3>();
                foreach(var face in body.Faces)foreach(float3 point in face.Points)
                    if(point.y>.62f*heightRatio)top.Add(point);
                if(top.Count==0)break;
                float3 vertex=top[random.Count(0,top.Count-1)],normal=float3.zero;
                foreach(var face in body.Faces)
                    if(math.abs(math.dot(face.Normal,vertex-face.Points[0]))<body.Epsilon*8)
                        normal+=face.Normal*random.Next(.5f,1.4f);
                if(math.lengthsq(normal)<.01f)continue;
                normal=math.normalize(normal);
                float depth=math.clamp(settings.chipDepth,.025f,.16f)*random.Next(.65f,1.2f);
                Optional(ref body,normal,math.dot(normal,vertex)-depth,output);
            }
            if(!low)
            {
                // Adjacency is captured from the current coarse solid, never inferred
                // from angular order alone. Detail cuts have independent randomness.
                var detail=new RockRandom(unchecked(seed^0x2f36a91));int count=0;
                foreach(var pair in body.Adjacencies())
                {
                    if(count>=14)break;
                    if(detail.Next(0,1)<.30f)continue;
                    float3 sum=pair.Item1.Normal+pair.Item2.Normal;float length=math.length(sum);
                    float d=(math.dot(pair.Item1.Normal,pair.Item1.Points[0])+math.dot(pair.Item2.Normal,pair.Item2.Points[0]))/length;
                    float inset=math.clamp(settings.bevelInset,.015f,.07f)*detail.Next(.45f,1.35f);
                    Optional(ref body,sum/length,d-inset,output);count++;
                }
            }
            if(!body.Validate(out _))throw new InvalidOperationException("Invalid rock solid after clipping seed "+seed);
            output.Parts.Add(new RockShapeData.Part{Solid=body,Scale=scale,Translation=translation,
                Rotation=quaternion.EulerXYZ(0,random.Next(-.1f,.1f),lean),Tone=random.Next(.97f,1.03f)});
        }
        private static void Required(ref RockPolyhedron body,float3 normal,float distance)
        {
            // Redundant asymmetric halfspaces already contain the current solid.
            bool alreadyInside=true;
            foreach(var face in body.Faces)foreach(float3 point in face.Points)
                if(math.dot(normal,point)>distance){alreadyInside=false;break;}
            if(alreadyInside)return;
            if(!body.Clip(normal,distance,out var clipped))throw new InvalidOperationException("Required prism plane failed validation.");
            body=clipped;
        }
        private static void Optional(ref RockPolyhedron body,float3 normal,float distance,RockShapeData report)
        {
            body.Validate(out double before);
            if(body.Clip(normal,distance,out var clipped) && clipped.Validate(out double after) && after>before*.76 && clipped.MinimumTriangleCross()>clipped.Epsilon*15)
                body=clipped;
            else report.RejectedCuts++;
        }
    }
}
