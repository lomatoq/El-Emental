using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.Geometry;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Elemental.Authoring.Editor
{
    /// <summary>Source/physics mesh publication only. Never rebuilds a scene or edits materials.</summary>
    public static class HardPolishRockBake
    {
        private const string Root="Assets/Elemental/Content/GraphicsV5/";
        private const string ReportPath="BuildReports/HardPolish/G01/Bake.json";
        private static readonly HashSet<int> PhysicsIndices=new HashSet<int>{0,1,2,3,4,5,6,7,12,17,18,19};
        [Serializable] public sealed class Entry
        {
            public string path,guidBefore,guidAfter,diagnostics;
            public int triangles,vertices;
            public double signedVolume;
        }
        [Serializable] public sealed class Report
        {
            public string utc,error;
            public bool published,rolledBack;
            public double elapsedMilliseconds;
            public List<Entry> entries=new List<Entry>();
        }
        private sealed class Snapshot
        {
            public Mesh asset,backup;
        }
        [MenuItem("Elemental/QA/Hard Polish/G01 Validate And Rebuild Rock Meshes Only")]
        public static void Rebuild()
        {
            var report=new Report{utc=DateTime.UtcNow.ToString("O")};
            var timer=Stopwatch.StartNew();
            var snapshots=new List<Snapshot>();
            bool startedPublication=false;
            try
            {
                Preflight(report,snapshots);
                Type builder=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GraphicsV5Slice1Builder",false)).FirstOrDefault(t=>t!=null);
                MethodInfo rebuild=builder?.GetMethod("RebuildRockLibrary",BindingFlags.Public|BindingFlags.Static);
                if(rebuild==null)throw new InvalidOperationException("Existing GraphicsV5Slice1Builder.RebuildRockLibrary entry is unavailable.");
                startedPublication=true;
                rebuild.Invoke(null,null);
                RumblePhysicsRockAssetBuilder.CreateOrUpdateHeroLibrary();
                RumblePhysicsRockAssetBuilder.CreateOrUpdateDebrisLibrary();
                foreach(Entry entry in report.entries)
                {
                    entry.guidAfter=AssetDatabase.AssetPathToGUID(entry.path);
                    if(entry.guidAfter!=entry.guidBefore)throw new InvalidOperationException("Mesh identity changed: "+entry.path);
                    Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(entry.path);
                    var policy=new EarthMeshIntegrityPolicy(true,true,false,entry.path.Contains("/Physics/")?255:4096,
                        weldTolerance:0.000001f,strictFlatNormals:true);
                    var integrity=EarthMeshIntegrityValidator.Validate(mesh,policy);
                    if(!integrity.IsValid)throw new InvalidOperationException(entry.path+": "+integrity);
                    entry.triangles=integrity.TriangleCount;entry.vertices=integrity.VertexCount;entry.signedVolume=integrity.SignedVolume;
                }
                AssetDatabase.SaveAssets();report.published=true;
            }
            catch(Exception exception)
            {
                report.error=(exception is TargetInvocationException invocation&&invocation.InnerException!=null?invocation.InnerException:exception).ToString();
                if(startedPublication)
                {
                    foreach(Snapshot snapshot in snapshots)
                    {
                        EditorUtility.CopySerialized(snapshot.backup,snapshot.asset);
                        EditorUtility.SetDirty(snapshot.asset);
                    }
                    AssetDatabase.SaveAssets();report.rolledBack=true;
                }
                throw;
            }
            finally
            {
                foreach(Snapshot snapshot in snapshots)if(snapshot.backup!=null)Object.DestroyImmediate(snapshot.backup);
                timer.Stop();report.elapsedMilliseconds=timer.Elapsed.TotalMilliseconds;
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
            }
        }
        public static Report ValidateCandidateLibrary()
        {
            var report=new Report{utc=DateTime.UtcNow.ToString("O")};
            var snapshots=new List<Snapshot>();
            try{Preflight(report,snapshots);return report;}
            finally{foreach(Snapshot snapshot in snapshots)Object.DestroyImmediate(snapshot.backup);}
        }
        private static void Preflight(Report report,List<Snapshot> snapshots)
        {
            for(int index=0;index<20;index++)
            {
                RumbleRockFamily family=index<8?RumbleRockFamily.Boulder:index<12?RumbleRockFamily.Slab:index<16?RumbleRockFamily.Wedge:RumbleRockFamily.Pebble;
                int seed=51803+index*7919;
                float scale=index<3?1.35f:index<8?1.1f:index<16?1.22f:.62f;
                string name=$"V5_{family}_{index:00}";
                var recipe=RumbleRockMeshFactory.CreateDefaultRecipe(seed,family,scale);
                Mesh candidate=RumbleRockMeshFactory.Build(recipe,out var diagnostics,name);
                try
                {
                    AddEntry(Root+"Rocks/"+name+".asset",candidate,4096,diagnostics.ToString(),report,snapshots);
                    if(!PhysicsIndices.Contains(index))continue;
                    Bounds bounds=candidate.bounds;
                    float divisor=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
                    Vector3[] vertices=candidate.vertices;
                    for(int vertex=0;vertex<vertices.Length;vertex++)vertices[vertex]=(vertices[vertex]-bounds.center)/divisor;
                    candidate.vertices=vertices;candidate.RecalculateBounds();
                    string physicsName=name.Replace("V5_","V5_Physics_")+"_CenteredUnit";
                    AddEntry(Root+"Physics/"+physicsName+".asset",candidate,255,diagnostics.ToString(),report,snapshots);
                }
                finally{Object.DestroyImmediate(candidate);}
            }
        }
        private static void AddEntry(string path,Mesh candidate,int budget,string diagnostics,Report report,List<Snapshot> snapshots)
        {
            var policy=new EarthMeshIntegrityPolicy(true,true,false,budget,weldTolerance:0.000001f,strictFlatNormals:true);
            var integrity=EarthMeshIntegrityValidator.Validate(candidate,policy);
            if(!integrity.IsValid)throw new InvalidOperationException("Preflight rejected before any asset write: "+path+"; "+integrity+"; "+diagnostics);
            Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            string guid=AssetDatabase.AssetPathToGUID(path);
            if(existing==null||string.IsNullOrEmpty(guid))throw new InvalidOperationException("Expected existing mesh identity is missing: "+path);
            snapshots.Add(new Snapshot{asset=existing,backup=Object.Instantiate(existing)});
            report.entries.Add(new Entry{path=path,guidBefore=guid,diagnostics=diagnostics,
                triangles=integrity.TriangleCount,vertices=integrity.VertexCount,signedVolume=integrity.SignedVolume});
        }
    }
}
