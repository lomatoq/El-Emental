using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Elemental.Runtime.Geometry;
using UnityEditor;
using UnityEngine;
namespace Elemental.Authoring.Editor
{
    public static class HardPolishStoneRouteAudit
    {
        public static string Run(string reportPath)
        {
            var report=new StringBuilder();int assets=0,assetFailures=0,scene=0,sceneFailures=0;
            foreach(string folder in new[]{"Assets/Elemental/Content/GraphicsV5/Rocks","Assets/Elemental/Content/GraphicsV5/Physics"})
                foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{folder}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(mesh==null||!mesh.name.StartsWith("V5_",StringComparison.Ordinal))continue;
                    var result=EarthMeshIntegrityValidator.Validate(mesh,new EarthMeshIntegrityPolicy(true,true,false,100000,weldTolerance:.000001f,strictFlatNormals:true));
                    assets++;if(!result.IsValid)assetFailures++;report.AppendLine("ASSET "+path+" "+result);
                }
            var tested=new Dictionary<Mesh,string>();
            foreach(var filter in Resources.FindObjectsOfTypeAll<MeshFilter>())
            {
                if(!filter.gameObject.scene.IsValid()||!filter.gameObject.scene.isLoaded||filter.sharedMesh==null)continue;
                var renderer=filter.GetComponent<Renderer>();if(renderer==null)continue;
                bool stone=false;foreach(var material in renderer.sharedMaterials)
                    if(material!=null&&material.shader!=null&&material.shader.name.IndexOf("Rock",StringComparison.OrdinalIgnoreCase)>=0){stone=true;break;}
                if(!stone)continue;Mesh mesh=filter.sharedMesh;
                if(!tested.TryGetValue(mesh,out string result))
                {
                    result=EarthMeshIntegrityValidator.Validate(mesh,new EarthMeshIntegrityPolicy(true,true,false,100000,weldTolerance:.00001f,strictFlatNormals:true)).ToString();
                    tested.Add(mesh,result);
                }
                scene++;if(!EarthContainedRenderRepair.IsClosed(mesh))sceneFailures++;
                var collider=filter.GetComponent<MeshCollider>();
                report.AppendLine("SCENE "+Hierarchy(filter.transform)+" active="+filter.gameObject.activeInHierarchy+" mesh="+mesh.name+" asset="+AssetDatabase.GetAssetPath(mesh)+
                    " collider="+(collider!=null&&collider.sharedMesh!=null?collider.sharedMesh.name:"none-on-render-object")+" "+result);
            }
            string summary=$"assets={assets} assetIntegrityFailures={assetFailures} sceneRenderers={scene} uniqueSceneMeshes={tested.Count} sceneOpenClosureCount={sceneFailures}\n";
            report.Insert(0,summary);string directory=Path.GetDirectoryName(reportPath);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);File.WriteAllText(reportPath,report.ToString());return summary;
        }
        private static string Hierarchy(Transform value){string path=value.name;while(value.parent!=null){value=value.parent;path=value.name+"/"+path;}return path;}
    }
}
