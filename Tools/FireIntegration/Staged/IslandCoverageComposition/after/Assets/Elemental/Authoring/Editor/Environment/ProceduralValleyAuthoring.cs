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
        [MenuItem("Elemental/Environment/Procedural Valley/4 Enable Rear Continuation")]
        public static void EnableRearContinuation()
        {var owner=Owner();Undo.RecordObject(owner.profile,"Add menu-side valley continuation");owner.profile.rearContinuation=true;EditorUtility.SetDirty(owner.profile);Regenerate();}
        [MenuItem("Elemental/Environment/Procedural Valley/5 Compose Main And Combat Views")]
        public static void ComposeViews()
        {
            var owner=Owner();var profile=owner.profile;
            int[] variants={0,4,8,2,6,10};var meshes=new Mesh[variants.Length];
            for(int i=0;i<variants.Length;i++)
            {
                string path=Root+"/Preview/Pillar_"+variants[i].ToString("00")+".asset";
                meshes[i]=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(meshes[i]==null)throw new InvalidOperationException("Accepted pillar preview mesh missing: "+path+". Do not substitute a new shape.");
            }
            Undo.RecordObject(profile,"Compose decorative landmarks for Main and Combat");
            profile.viewGroundPillars=meshes;
            if(profile.viewLandmarks==null||profile.viewLandmarks.Length==0)profile.viewLandmarks=DistantBackdropProfile.DefaultViewLandmarks();
            profile.viewComposition=true;profile.rearContinuation=true;EditorUtility.SetDirty(profile);Regenerate();
        }
        [MenuItem("Elemental/Environment/Procedural Valley/6 Refine Floating Cores And Depth Layers")]
        public static void RefineFloatingAndLayers()
        {
            var owner=Owner();var profile=owner.profile;
            if(!profile.viewComposition||profile.viewLandmarks==null||profile.viewLandmarks.Length<12)
                throw new InvalidOperationException("Compose the accepted Main and Combat landmarks first.");
            // Only island assets change. Accepted ground meshes and existing authored
            // positions, scales and material references remain intact.
            for(int family=0;family<6;family++)for(int lod=0;lod<2;lod++)
            {
                int i=family+6;
                Mesh generated=ProceduralRockMesh.Group(unchecked(profile.geometrySeed+i*131),2+family%4,true,family,lod==1,profile.rockShape);
                Mesh mesh=SaveMesh(generated,Root+"/Meshes/Island_"+i+"_LOD"+lod+".asset");
                if(lod==0)profile.islandSilhouettes[family]=mesh;else profile.islandLodSilhouettes[family]=mesh;
            }
            var slots=new List<DistantBackdropProfile.ViewLandmark>(profile.viewLandmarks);
            var additions=new[]{
                Slot("MainUpper0",true,1,new Vector3(700,430,-1500),220),
                Slot("MainUpper1",true,4,new Vector3(-340,555,-1710),205),
                Slot("MainUpper2",true,2,new Vector3(355,675,-1930),245),
                Slot("CombatUpper0",true,3,new Vector3(-660,445,1530),225),
                Slot("CombatUpper1",true,0,new Vector3(570,565,1710),215),
                Slot("CombatUpper2",true,5,new Vector3(-75,690,1910),240),
                Slot("MainInfill0",false,4,new Vector3(225,-155,-655),65),
                Slot("MainInfill1",false,1,new Vector3(465,-155,-820),86),
                Slot("MainInfill2",false,3,new Vector3(-30,-155,-930),73),
                Slot("MainInfill3",false,5,new Vector3(650,-155,-1070),95),
                Slot("MainInfill4",false,2,new Vector3(260,-155,-1210),84),
                Slot("MainInfill5",false,0,new Vector3(-340,-155,-1350),108)};
            foreach(var addition in additions)
                if(!slots.Exists(x=>x.name==addition.name))slots.Add(addition);
            if(slots.Count>32)throw new InvalidOperationException("The bounded authored landmark budget is 32.");
            Undo.RecordObject(profile,"Add atmospheric upper islands and Main ground depth");
            profile.viewLandmarks=slots.ToArray();EditorUtility.SetDirty(profile);
            Regenerate();AssetDatabase.SaveAssets();
        }
        [MenuItem("Elemental/Environment/Procedural Valley/7 Add Small Island Companions")]
        public static void AddIslandCompanions()
        {
            var owner=Owner();var profile=owner.profile;
            if(!profile.viewComposition||profile.viewLandmarks==null||profile.viewLandmarks.Length<24)
                throw new InvalidOperationException("Install the approved core and depth composition first.");
            var slots=new List<DistantBackdropProfile.ViewLandmark>(profile.viewLandmarks);
            // Absolute assignments make repeating the menu idempotent. Existing mesh
            // references, all ground landmarks and all other island poses are retained.
            SetCompanionPrimary(slots,"MainIsland0",new Vector3(94.1744f,62.521f,-461.2588f),216.9668f);
            SetCompanionPrimary(slots,"CombatIsland0",new Vector3(-160.287f,62.051f,459.2957f),210.6227f);
            var additions=new[]{
                Slot("MainSideSmall0",true,3,new Vector3(470,235,-860),95),
                Slot("MainSideSmall1",true,1,new Vector3(-390,310,-1060),78),
                Slot("CombatSideSmall0",true,4,new Vector3(-455,260,875),90),
                Slot("CombatSideSmall1",true,2,new Vector3(440,340,1050),82),
                Slot("MainSatellite0",true,4,new Vector3(-18,150,-420),52),
                Slot("MainSatellite1",true,2,new Vector3(200,182,-510),37),
                Slot("CombatSatellite0",true,0,new Vector3(-270,146,420),49),
                Slot("CombatSatellite1",true,5,new Vector3(-55,187,510),35)};
            foreach(var addition in additions)if(!slots.Exists(x=>x.name==addition.name))slots.Add(addition);
            if(slots.Count>32)throw new InvalidOperationException("The bounded authored landmark budget is 32.");
            Undo.RecordObject(profile,"Add small decorative island companions");
            profile.viewLandmarks=slots.ToArray();EditorUtility.SetDirty(profile);
            Regenerate();AssetDatabase.SaveAssets();
        }
        [MenuItem("Elemental/Environment/Procedural Valley/8 Frame Visible Islands And Satellites")]
        public static void FrameVisibleIslandsAndSatellites()
        {
            var owner=Owner();var profile=owner.profile;
            if(!profile.viewComposition||profile.viewLandmarks==null||profile.viewLandmarks.Length!=32)
                throw new InvalidOperationException("Install the approved 32-slot companion composition first.");
            // Actual approved pillar bounds, Main 38-degree lens and Combat 60-degree
            // lens. Existing island slots are brought into useful screen space; no extra
            // geometry/count, camera change, material override or ground displacement.
            var slots=new List<DistantBackdropProfile.ViewLandmark>(profile.viewLandmarks);
            var visible=new[]{
                Slot("MainIsland0",true,0,new Vector3(140.563f,66.8802f,-405.5129f),171.3115f),
                Slot("MainIsland1",true,2,new Vector3(-124.4791f,120.1317f,-654.7544f),165.7306f),
                Slot("MainIsland2",true,5,new Vector3(47.5211f,206.1625f,-828.7792f),136.6577f),
                Slot("MainUpper0",true,1,new Vector3(-114.0955f,342.4973f,-1094.5268f),112.3731f),
                Slot("MainUpper1",true,4,new Vector3(348.0581f,233.1568f,-835.4612f),97.7483f),
                Slot("MainUpper2",true,2,new Vector3(207.0483f,333.7051f,-1155.4871f),80.7586f),
                Slot("MainSideSmall0",true,3,new Vector3(-174.4232f,185.0647f,-631.9514f),74.699f),
                Slot("MainSideSmall1",true,1,new Vector3(234.8135f,80.8381f,-462.0616f),85.9654f),
                Slot("MainSatellite0",true,4,new Vector3(175.9102f,113.2992f,-377.1155f),24.0161f),
                Slot("MainSatellite1",true,2,new Vector3(101.6117f,85.8656f,-433.9462f),24.3056f),
                Slot("CombatIsland0",true,1,new Vector3(-140.3138f,96.7112f,416.2799f),276.9792f),
                Slot("CombatIsland1",true,3,new Vector3(360.2105f,98.3501f,476.6869f),255.0571f),
                Slot("CombatIsland2",true,4,new Vector3(181.3395f,261.1755f,728.2286f),247.5061f),
                Slot("CombatUpper0",true,3,new Vector3(-612.4834f,375.2778f,862.6918f),150.5546f),
                Slot("CombatUpper1",true,0,new Vector3(890.8893f,413.3853f,967.3301f),198.3166f),
                Slot("CombatUpper2",true,5,new Vector3(-102.0821f,471.0462f,1070.2539f),150.3733f),
                Slot("CombatSideSmall0",true,4,new Vector3(-373.8883f,122.6047f,464.1638f),126.9451f),
                Slot("CombatSideSmall1",true,2,new Vector3(589.2595f,97.1114f,523.4536f),145.6166f),
                Slot("CombatSatellite0",true,0,new Vector3(-203.053f,171.2482f,394.8752f),50.101f),
                Slot("CombatSatellite1",true,5,new Vector3(-81.1244f,115.2416f,431.1316f),39.9086f)};
            Undo.RecordObject(profile,"Frame visible islands and close satellites");
            foreach(var item in visible)
            {
                int index=slots.FindIndex(x=>x.name==item.name);
                if(index<0||!slots[index].airborne)throw new InvalidOperationException("Approved island slot missing: "+item.name);
                var existing=slots[index];existing.position=item.position;existing.scale=item.scale;slots[index]=existing;
            }
            profile.viewLandmarks=slots.ToArray();EditorUtility.SetDirty(profile);
            Regenerate();AssetDatabase.SaveAssets();
        }
        private static void SetCompanionPrimary(List<DistantBackdropProfile.ViewLandmark> slots,string name,Vector3 position,float scale)
        {
            int index=slots.FindIndex(x=>x.name==name);
            if(index<0)throw new InvalidOperationException("Approved primary island is missing: "+name);
            var slot=slots[index];slot.position=position;slot.scale=scale;slots[index]=slot;
        }
        private static DistantBackdropProfile.ViewLandmark Slot(string name,bool airborne,int variant,Vector3 position,float scale)
            =>new DistantBackdropProfile.ViewLandmark{name=name,airborne=airborne,variant=variant,position=position,scale=scale};
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
            Undo.RecordObject(existing,"Update owned rock mesh");
            // CopySerialized can leave an existing renderer's GPU mesh buffer stale
            // even when CPU vertices compare equal. Explicit setters invalidate it.
            existing.Clear();existing.name=generated.name;existing.indexFormat=generated.indexFormat;
            existing.vertices=generated.vertices;existing.normals=generated.normals;
            existing.colors=generated.colors;existing.triangles=generated.triangles;
            existing.bounds=generated.bounds;existing.UploadMeshData(false);
            UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(existing);return existing;
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
