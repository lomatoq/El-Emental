using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Elemental.Runtime.Geometry;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    public static class HardPolishContainedRenderRepair
    {
        // Root supplies the exact selected cache asset and report destination.
        public static string Repair(string assetPath,string reportPath)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before repairing fracture render subassets.");
            var cache=AssetDatabase.LoadAssetAtPath<EarthConvexFractureCacheAsset>(assetPath);
            if(cache==null)throw new InvalidOperationException("No fracture cache at "+assetPath);
            var report=new StringBuilder();var seen=new HashSet<Mesh>();
            int repaired=0,fallbacks=0,removed=0;double start=EditorApplication.timeSinceStartup;
            try
            {
                foreach(var plan in cache.Plans)foreach(var piece in plan.Pieces)
                {
                    if(piece.Collider==null||piece.Render==null||piece.Collider==piece.Render)
                        throw new InvalidOperationException("Repair requires distinct collider/render references in every piece.");
                    if(!seen.Add(piece.Render))continue;
                    string colliderBefore=GeometrySignature(piece.Collider);
                    Mesh candidate=EarthContainedRenderRepair.Create(piece.Collider,out bool fallback,out int degenerate);
                    try
                    {
                        Validate(candidate);
                        string name=piece.Render.name;
                        EditorUtility.CopySerialized(candidate,piece.Render);piece.Render.name=name;
                        EditorUtility.SetDirty(piece.Render);
                        if(colliderBefore!=GeometrySignature(piece.Collider))throw new InvalidOperationException("Collider changed: "+piece.Collider.name);
                        repaired++;if(fallback)fallbacks++;removed+=degenerate;
                        if(fallback)report.AppendLine("HULL "+name);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(candidate);}
                }
                // Check every referenced render again after native serialized copies.
                foreach(Mesh mesh in seen)Validate(mesh);
                EditorUtility.SetDirty(cache);AssetDatabase.SaveAssetIfDirty(cache);
                report.Insert(0,$"SUCCESS renders={repaired} hullFallbacks={fallbacks} removedDegenerate={removed} closed=true normalsFiniteUnit=true cornerDotPositive=true collidersUnchanged=true seconds={EditorApplication.timeSinceStartup-start:F3}\n");
            }
            catch(Exception error)
            {
                report.Insert(0,$"FAILED after={repaired} hullFallbacks={fallbacks}; restore backed-up cache before continuing. {error}\n");
                Write();throw;
            }
            Write();return report.ToString();
            void Write(){string directory=Path.GetDirectoryName(reportPath);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);File.WriteAllText(reportPath,report.ToString());}
        }
        public static string Audit(string assetPath,string reportPath)
        {
            var cache=AssetDatabase.LoadAssetAtPath<EarthConvexFractureCacheAsset>(assetPath);
            if(cache==null)throw new InvalidOperationException("No fracture cache at "+assetPath);
            var seen=new HashSet<Mesh>();long triangles=0;
            foreach(var plan in cache.Plans)foreach(var piece in plan.Pieces)
                if(seen.Add(piece.Render)){Validate(piece.Render);triangles+=piece.Render.triangles.Length/3;}
            string result=$"SUCCESS meshes={seen.Count} triangles={triangles} invalidNormals=0 opposingCorners=0 degenerateTriangles=0 openOrNonManifoldMeshes=0 seamIdentity=exactFloatCoordinates";
            string directory=Path.GetDirectoryName(reportPath);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            File.WriteAllText(reportPath,result);return result;
        }
        public static void Validate(Mesh mesh)
        {
            if(!EarthContainedRenderRepair.IsClosed(mesh))throw new InvalidOperationException(mesh.name+": exact-coordinate render surface is open or non-manifold.");
            var vertices=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;
            if(normals.Length!=vertices.Length)throw new InvalidOperationException(mesh.name+": missing normals.");
            for(int i=0;i<normals.Length;i++)
                if(!float.IsFinite(normals[i].x)||!float.IsFinite(normals[i].y)||!float.IsFinite(normals[i].z)||Mathf.Abs(normals[i].sqrMagnitude-1f)>.001f)
                    throw new InvalidOperationException(mesh.name+": invalid normal "+i);
            for(int t=0;t<indices.Length;t+=3)
            {
                if(!EarthContainedRenderRepair.TryGeometricNormal(vertices[indices[t]],vertices[indices[t+1]],vertices[indices[t+2]],out Vector3 face))
                    throw new InvalidOperationException(mesh.name+": degenerate triangle "+t/3);
                for(int c=0;c<3;c++)if(Vector3.Dot(face,normals[indices[t+c]])<.999f)
                    throw new InvalidOperationException(mesh.name+": opposing/non-flat triangle corner "+(t+c));
            }
        }
        private static string GeometrySignature(Mesh mesh)
        {
            ulong hash=14695981039346656037UL;
            unchecked{foreach(var p in mesh.vertices){hash=(hash^(uint)BitConverter.SingleToInt32Bits(p.x))*1099511628211UL;hash=(hash^(uint)BitConverter.SingleToInt32Bits(p.y))*1099511628211UL;hash=(hash^(uint)BitConverter.SingleToInt32Bits(p.z))*1099511628211UL;}foreach(int index in mesh.triangles)hash=(hash^(uint)index)*1099511628211UL;}
            return hash.ToString("X16");
        }
    }
}
