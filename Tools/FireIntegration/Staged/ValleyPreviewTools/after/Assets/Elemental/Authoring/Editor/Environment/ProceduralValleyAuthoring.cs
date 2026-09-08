using System;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.DistantScenery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class ProceduralValleyAuthoring
    {
        private const string Root="Assets/Elemental/Content/Environment/DistantStone";
        private const string PreviewName="EE_RockPreview_V2";
        private const string ArenaMaterial="Assets/Elemental/Content/GraphicsV5/Materials/RumbleArenaSandstone.mat";
        [Serializable] private sealed class Evidence
        {public int seed,meshCount,totalTriangles;public List<string> meshes=new List<string>();public List<int> triangles=new List<int>();}

        [MenuItem("Elemental/Environment/Procedural Valley/1 Preview Twelve Pillars")]
        public static void PreviewTwelve()
        {
            DistantBackdrop owner=Owner();Material material=Arena();EnsureFolder(Root+"/Preview");
            ClearPreview(owner);
            var preview=new GameObject(PreviewName);preview.transform.SetParent(owner.transform,false);
            Vector3 up=owner.stagingUp.normalized,forward=Vector3.ProjectOnPlane(owner.profile.heroViewDirection,up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.forward+Vector3.right,up).normalized;
            Vector3 right=Vector3.Cross(up,forward).normalized;
            // Dedicated viewing grid outside the playable arena. No camera, lighting,
            // fog or arena object is repositioned or reconfigured by this preview.
            preview.transform.SetPositionAndRotation(owner.planetCenter.position+right*(owner.profile.planetRadius+80)+up*owner.profile.planetRadius,Quaternion.LookRotation(forward,up));
            var report=new Evidence{seed=owner.profile.geometrySeed};
            for(int i=0;i<12;i++)
            {
                int seed=unchecked(owner.profile.geometrySeed+i*1013);
                Mesh mesh=SaveMesh(ProceduralRockMesh.Pillar(seed,false,owner.profile.rockShape),Root+"/Preview/Pillar_"+i.ToString("00")+".asset");
                var go=new GameObject("Pillar "+i.ToString("00")+" seed "+seed);go.transform.SetParent(preview.transform,false);go.transform.localPosition=new Vector3((i%4)*2.8f,0,(i/4)*5.5f);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.receiveShadows=true;
                report.meshCount++;report.totalTriangles+=mesh.triangles.Length/3;report.meshes.Add(mesh.name);report.triangles.Add(mesh.triangles.Length/3);
            }
            Undo.RegisterCreatedObjectUndo(preview,"Preview twelve rock pillars");
            Directory.CreateDirectory("BuildReports/ProceduralValleyV2");File.WriteAllText("BuildReports/ProceduralValleyV2/preview-meshes.json",JsonUtility.ToJson(report,true));
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);Selection.activeObject=preview;
        }
        [MenuItem("Elemental/Environment/Procedural Valley/2 Bake Group Meshes")]
        public static void BakeMeshes()
        {
            DistantBackdrop owner=Owner();DistantBackdropProfile profile=owner.profile;Material material=Arena();
            EnsureFolder(Root+"/Meshes");
            var ground=new Mesh[6];var lowGround=new Mesh[6];var floating=new Mesh[6];var lowFloating=new Mesh[6];
            var report=new Evidence{seed=profile.geometrySeed};
            for(int i=0;i<12;i++)for(int lod=0;lod<2;lod++)
            {
                bool island=i>=6;int family=island?i-6:i;
                int count=island?2+family%4:Mathf.Clamp(profile.pillarsPerGroup+(family%3)-1,3,7);
                string path=Root+"/Meshes/"+(island?"Island_":"Massif_")+i+"_LOD"+lod+".asset";
                Mesh generated=ProceduralRockMesh.Group(unchecked(profile.geometrySeed+i*131),count,island,family,lod==1,profile.rockShape);
                Mesh mesh=SaveMesh(generated,path);
                if(island){if(lod==0)floating[family]=mesh;else lowFloating[family]=mesh;}
                else{if(lod==0)ground[i]=mesh;else lowGround[i]=mesh;}
                report.meshCount++;report.totalTriangles+=mesh.triangles.Length/3;report.meshes.Add(path);report.triangles.Add(mesh.triangles.Length/3);
            }
            Undo.RecordObject(profile,"Bake procedural valley mesh library");
            profile.silhouettes=ground;profile.lodSilhouettes=lowGround;profile.islandSilhouettes=floating;profile.islandLodSilhouettes=lowFloating;
            profile.material=material;profile.proceduralValley=true;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("BuildReports/ProceduralValleyV2");File.WriteAllText("BuildReports/ProceduralValleyV2/baked-meshes.json",JsonUtility.ToJson(report,true));
            // Separate regeneration step deliberately preserves the twelve-pillar
            // inspection gate. Baking never silently fills the existing scene.
        }
        [MenuItem("Elemental/Environment/Procedural Valley/3 Regenerate Same Seed")]
        public static void Regenerate()
        {
            var owner=Owner();if(!owner.profile.proceduralValley)throw new InvalidOperationException("Inspect twelve pillars, then bake group meshes first.");
            owner.profile.material=Arena();EditorUtility.SetDirty(owner.profile);
            ClearPreview(owner);owner.animateWhenPaused=true;owner.Rebuild();
            EditorUtility.SetDirty(owner);EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            if(owner.RejectedPlacements>0)Debug.LogWarning("Valley excluded "+owner.RejectedPlacements+" groups by full bounds. Inspect exclusions before accepting the composition.",owner);
        }
        [MenuItem("Elemental/Environment/Procedural Valley/New Placement Seed")]
        public static void NewSeed()
        {var owner=Owner();Undo.RecordObject(owner.profile,"Reseed valley placement");owner.profile.seed=unchecked((int)RockRandom.Hash((uint)owner.profile.seed+1));Regenerate();}
        [MenuItem("Elemental/Environment/Procedural Valley/Clear Generated Objects")]
        public static void Clear()
        {var owner=Owner();ClearPreview(owner);owner.ClearGenerated();EditorUtility.SetDirty(owner);EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);}
        [MenuItem("Elemental/Environment/Procedural Valley/Clear Preview Only")]
        public static void ClearPreviewOnly()
        {
            var owner=Owner();ClearPreview(owner);
            EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
        }
        private static Mesh SaveMesh(Mesh generated,string path)
        {
            if(!path.StartsWith(Root+"/",StringComparison.Ordinal))throw new InvalidOperationException("Mesh path is outside the owned backdrop asset folder.");
            Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(generated,path);return generated;}
            Undo.RecordObject(existing,"Update owned rock mesh");EditorUtility.CopySerialized(generated,existing);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(existing);return existing;
        }
        private static void ClearPreview(DistantBackdrop owner)
        {Transform child=owner.transform.Find(PreviewName);if(child!=null)Undo.DestroyObjectImmediate(child.gameObject);}
        private static Material Arena()
        {var material=AssetDatabase.LoadAssetAtPath<Material>(ArenaMaterial);if(material==null)throw new InvalidOperationException("Existing arena material required; no replacement material is created.");return material;}
        private static DistantBackdrop Owner()
        {
            Scene scene=SceneManager.GetActiveScene();if(Application.isPlaying || scene.name!="EarthCoreSlice")throw new InvalidOperationException("Open EarthCoreSlice in Edit Mode.");
            DistantBackdrop owner=null;foreach(var root in scene.GetRootGameObjects())foreach(var candidate in root.GetComponentsInChildren<DistantBackdrop>(true))
            {if(owner!=null)throw new InvalidOperationException("Multiple backdrop owners.");owner=candidate;}
            if(owner==null || owner.profile==null || owner.planetCenter==null || AssetDatabase.GetAssetPath(owner.profile)!=Root+"/DistantBackdropProfile.asset")throw new InvalidOperationException("Install the existing owned backdrop before using its procedural tools.");return owner;
        }
        private static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
    }
}
