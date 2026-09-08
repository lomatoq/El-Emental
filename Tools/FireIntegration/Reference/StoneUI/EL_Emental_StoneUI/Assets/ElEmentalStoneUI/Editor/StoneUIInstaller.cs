using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ElEmental.StoneUI.Editor
{
    public static class StoneUIInstaller
    {
        public const string Root="Assets/ElEmentalStoneUI";
        private const string Generated=Root+"/Generated";
        private const string ResourceRoot=Root+"/Resources/ElEmentalStoneUI";
        private static T Load<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        private static void Folder(string path){Directory.CreateDirectory(path);AssetDatabase.Refresh();}
        [MenuItem("Tools/EL EMENTAL/Stone UI/1 - Build library and prefabs")]
        public static void BuildLibrary()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Leave Play Mode before building the UI library.");return;}
            Folder(Generated+"/Prefabs");Folder(Generated+"/Meshes");Folder(Generated+"/Materials");Folder(ResourceRoot);
            var library=Load<StoneUIAssets>(ResourceRoot+"/StoneUIAssets.asset");
            if(library==null){library=ScriptableObject.CreateInstance<StoneUIAssets>();AssetDatabase.CreateAsset(library,ResourceRoot+"/StoneUIAssets.asset");}
            library.art=Directory.GetFiles(Root+"/Art","*.png",SearchOption.AllDirectories)
                .Where(p=>!p.Replace('\\','/').Contains("/SourceOnly/"))
                .Select(p=>new StoneUIAssets.ArtEntry{id=Path.GetFileNameWithoutExtension(p),texture=Load<Texture2D>(p.Replace('\\','/'))}).ToArray();
            library.screens=Directory.GetFiles(Root+"/UI/Screens","*.uxml").Select(p=>new StoneUIAssets.ScreenEntry{id=Path.GetFileNameWithoutExtension(p),tree=Load<VisualTreeAsset>(p.Replace('\\','/'))}).ToArray();
            library.sounds=Directory.Exists(Root+"/Audio")?Directory.GetFiles(Root+"/Audio","*.wav").Select(p=>new StoneUIAssets.SoundEntry{id=Path.GetFileNameWithoutExtension(p),clip=Load<AudioClip>(p.Replace('\\','/'))}).ToArray():Array.Empty<StoneUIAssets.SoundEntry>();
            library.theme=Load<StyleSheet>(Root+"/UI/StoneTheme.uss");
            library.demoBackground=Load<Texture2D>(Root+"/Art/Preview/demo_world.jpg");
            library.uiFxShader=Load<Shader>(Root+"/Shaders/StoneUIFX.shader");
            library.animationPresets=Load<TextAsset>(Root+"/Config/animation_presets.json");
            if(library.displayFont==null)library.displayFont=FindFont("Cinzel");
            if(library.bodyFont==null)library.bodyFont=FindFont("Manrope");
            EditorUtility.SetDirty(library);
            var panel=Load<PanelSettings>(Generated+"/StonePanelSettings.asset");
            if(panel==null){panel=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(panel,Generated+"/StonePanelSettings.asset");}
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1920,1080);panel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;panel.match=1;
            panel.sortingOrder=100;panel.clearColor=false;panel.themeStyleSheet=Load<ThemeStyleSheet>(Root+"/UI/StoneDefault.tss");EditorUtility.SetDirty(panel);
            SaveRootPrefab(library,panel,false);SaveRootPrefab(library,panel,true);
            BuildEnvironmentAssets();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("StoneUI library built: "+library.art.Length+" textures, "+library.screens.Length+" screens. Prefabs under "+Generated+"/Prefabs. No existing scene or gameplay file was changed.");
            if(library.displayFont==null||library.bodyFont==null)Debug.LogWarning("Assign separately obtained fonts on the StoneUIAssets asset. The preview runs with Unity's fallback font; font files are not bundled.");
            Selection.activeObject=library;
        }
        private static Font FindFont(string family)
        {
            foreach(string guid in AssetDatabase.FindAssets(family+" t:Font")){var f=Load<Font>(AssetDatabase.GUIDToAssetPath(guid));if(f!=null)return f;}return null;
        }
        private static void SaveRootPrefab(StoneUIAssets library,PanelSettings panel,bool demo)
        {
            var go=new GameObject(demo?"StoneUI_Demo":"StoneUI_Root");go.SetActive(false);
            try
            {
                var document=go.AddComponent<UIDocument>();document.panelSettings=panel;document.sortingOrder=100;
                go.AddComponent<StoneUIMotion>();var sounds=go.AddComponent<StoneUISounds>();sounds.assets=library;
                var controller=go.AddComponent<StoneUIController>();controller.assets=library;controller.demoMode=demo;controller.initialScreen="MainMenu";
                go.SetActive(true);PrefabUtility.SaveAsPrefabAsset(go,Generated+"/Prefabs/"+(demo?"StoneUI_Demo":"StoneUI_Root")+".prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        private static void BuildEnvironmentAssets()
        {
            var profile=Load<DistantBackdropProfile>(Generated+"/DistantBackdropProfile.asset");
            if(profile==null){profile=ScriptableObject.CreateInstance<DistantBackdropProfile>();AssetDatabase.CreateAsset(profile,Generated+"/DistantBackdropProfile.asset");}
            profile.silhouettes=new Mesh[8];profile.lodSilhouettes=new Mesh[8];
            for(int i=0;i<8;i++)
            {
                foreach(int lod in new[]{0,1})
                {
                    string path=Generated+"/Meshes/rock_"+i.ToString("00")+"_LOD"+lod+".asset";var mesh=Load<Mesh>(path);
                    if(mesh==null){mesh=ProceduralRockMesh.Create(620+i*13,lod==0?9+i%4:6,i>=4,lod==0?6:4);AssetDatabase.CreateAsset(mesh,path);}
                    if(lod==0)profile.silhouettes[i]=mesh;else profile.lodSilhouettes[i]=mesh;
                }
            }
            var material=Load<Material>(Generated+"/Materials/DistantStone.mat");
            if(material==null)
            {
                Shader shader=Load<Shader>(Root+"/Shaders/DistantStoneURP.shader");
                if(shader!=null){material=new Material(shader){name="EE_DistantStone",enableInstancing=true};material.SetTexture("_DetailTex",Load<Texture2D>(Root+"/Art/Environment/distant_stone_detail.png"));AssetDatabase.CreateAsset(material,Generated+"/Materials/DistantStone.mat");}
            }
            profile.material=material;EditorUtility.SetDirty(profile);
        }
        [MenuItem("Tools/EL EMENTAL/Stone UI/2 - Build UI demo scene")]
        public static void BuildUIDemo()
        {
            BuildLibrary();if(EditorApplication.isPlaying)return;
            SavePreviewScene(false);
        }
        [MenuItem("Tools/EL EMENTAL/Stone UI/3 - Build distant-world preview scene")]
        public static void BuildWorldDemo()
        {
            BuildLibrary();if(EditorApplication.isPlaying)return;
            SavePreviewScene(true);
        }
        private static void SavePreviewScene(bool world)
        {
            Folder(Generated+"/Scenes");Scene previous=SceneManager.GetActiveScene();Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var cameraGO=new GameObject("Preview Camera");var camera=cameraGO.AddComponent<Camera>();cameraGO.AddComponent<AudioListener>();
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.39f,.60f,.80f);camera.nearClipPlane=.15f;camera.farClipPlane=3000;camera.fieldOfView=58;
                if(!world)
                {
                    var prefab=Load<GameObject>(Generated+"/Prefabs/StoneUI_Demo.prefab");PrefabUtility.InstantiatePrefab(prefab,scene);
                }
                else
                {
                    var planet=GameObject.CreatePrimitive(PrimitiveType.Sphere);planet.name="Preview ONLY - radius 36 stand-in";planet.transform.localScale=Vector3.one*72;
                    UnityEngine.Object.DestroyImmediate(planet.GetComponent<Collider>());
                    var root=new GameObject("EE_DistantBackdrop_Preview");var backdrop=root.AddComponent<DistantBackdrop>();backdrop.profile=Load<DistantBackdropProfile>(Generated+"/DistantBackdropProfile.asset");backdrop.planetCenter=planet.transform;backdrop.Rebuild();
                    camera.transform.position=new Vector3(0,42,-13);camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,.10f,1));
                    var sunGO=new GameObject("Preview Sun");var sun=sunGO.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.color=new Color(1,.91f,.79f);sun.transform.rotation=Quaternion.Euler(38,-24,0);RenderSettings.sun=sun;RenderSettings.ambientLight=new Color(.37f,.45f,.58f);
                }
                string path=Generated+"/Scenes/"+(world?"DistantWorld_Preview":"StoneUI_Demo")+".unity";EditorSceneManager.SaveScene(scene,path);Debug.Log("Saved "+path+". Open it separately to preview; your current scene is unchanged.");
            }
            finally{if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("Tools/EL EMENTAL/Stone UI/4 - Validate imported library")]
        public static void ValidateLibrary()
        {
            var library=Load<StoneUIAssets>(ResourceRoot+"/StoneUIAssets.asset");if(library==null){Debug.LogError("Build the library first.");return;}
            int errors=0;
            foreach(var e in library.art)if(e.texture==null){Debug.LogError("Missing texture: "+e.id);errors++;}
            foreach(var s in library.screens)
            {
                if(s.tree==null){Debug.LogError("Missing UXML: "+s.id);errors++;continue;}
                try{var root=s.tree.CloneTree();if(root.Q("screen-root")==null){Debug.LogError("Missing screen-root in "+s.id);errors++;}}
                catch(Exception ex){Debug.LogException(ex);errors++;}
            }
            if(library.uiFxShader==null){Debug.LogError("Missing optional UI FX shader reference.");errors++;}
            Debug.Log("StoneUI validation finished. Errors="+errors+". This is import/tree validation, not a substitute for a Play Mode and build test.");
        }
    }
}
