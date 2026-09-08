using Elemental.Presentation.Rendering;
using Elemental.Presentation.DistantScenery;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class CloudBankCardsSetup
    {
        [MenuItem("Elemental/Graphics/Install Cumulus Art Banks")]
        public static void Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying || scene.name!="EarthCoreSlice")throw new System.InvalidOperationException("Open nonplaying EarthCoreSlice for optional cloud-art installation.");
            VoxelPlanetBehaviour planet=null;DistantBackdrop backdrop=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                var p=root.GetComponentInChildren<VoxelPlanetBehaviour>(true);if(p!=null){if(planet!=null)throw new System.InvalidOperationException("Ambiguous planets.");planet=p;}
                backdrop=root.GetComponentInChildren<DistantBackdrop>(true)??backdrop;
            }
            if(planet==null || backdrop==null)throw new System.InvalidOperationException("Authored planet and distant staging frame required.");
            const string texturePath="Assets/Elemental/Content/Textures/Clouds/reference-bank-v1.png";
            var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if(importer==null)throw new System.InvalidOperationException("Import generated RGBA cloud bank first.");
            importer.textureType=TextureImporterType.Default;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);var shader=Shader.Find("Elemental/Cloud Bank Art");
            if(shader==null || ShaderUtil.ShaderHasError(shader))throw new System.InvalidOperationException("Cloud-art shader compilation must pass.");
            const string materialPath="Assets/Elemental/Content/Materials/CloudBankCards_v1.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",texture);EditorUtility.SetDirty(material);
            var existing=planet.transform.Find(CloudBankCards.OwnedName);
            if(existing!=null && existing.GetComponent<CloudBankCards>()==null)throw new System.InvalidOperationException("Cloud-art name belongs to another owner.");
            var go=existing!=null?existing.gameObject:new GameObject(CloudBankCards.OwnedName);
            if(existing==null)Undo.RegisterCreatedObjectUndo(go,"Install cloud art banks");
            var cards=go.GetComponent<CloudBankCards>();if(cards==null)cards=Undo.AddComponent<CloudBankCards>(go);
            cards.Configure(planet.transform,backdrop.stagingUp,planet.WorldProfile!=null?planet.WorldProfile.Radius:planet.Radius,Resources.GetBuiltinResource<Mesh>("Quad.fbx"),material);
            EditorUtility.SetDirty(cards);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Elemental/Graphics/Capture Cumulus Banks Current View")]
        public static void CaptureCurrentView()
        {
            var scene=SceneManager.GetActiveScene();CloudBankCards cards=null;CelestialSystemBehaviour sky=null;
            foreach(var root in scene.GetRootGameObjects()){cards=root.GetComponentInChildren<CloudBankCards>(true)??cards;sky=root.GetComponentInChildren<CelestialSystemBehaviour>(true)??sky;}
            if(cards==null || sky==null || sky.TargetCamera==null)throw new System.InvalidOperationException("Install cards and use production scene camera first.");
            var camera=sky.TargetCamera;bool active=cards.gameObject.activeSelf;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;float oldPhase=sky.Snapshot.TimeOfDay01;var oldAuthority=sky.LightingAuthority;
            var rt=new RenderTexture(1280,720,24);rt.Create();var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                System.IO.Directory.CreateDirectory("Logs/CloudBankCards");
                sky.SetLightingAuthorityForQa(Elemental.Simulation.Time.CelestialLightingAuthorityMode.AnimatedEphemeris);
                for(int phase=0;phase<2;phase++)for(int angle=0;angle<3;angle++)
                {
                    camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                    if(angle>0)
                    {
                        Vector3 up=cards.transform.up,centre=cards.transform.parent.position;
                        Vector3 tangent=Vector3.ProjectOnPlane(oldRotation*Vector3.forward,up).normalized;
                        if(tangent.sqrMagnitude<0.5f)tangent=cards.transform.forward;
                        camera.transform.position=centre+up*(angle==1?95:140)+tangent*(angle==1?125:380);
                        camera.transform.rotation=Quaternion.LookRotation(centre-up*(angle==1?160:40)-camera.transform.position,up);
                    }
                    sky.SetTimeOfDayForQa(phase==0?0.25f:0.75f);sky.EvaluatePresentationForQa();
                    for(int enabled=0;enabled<2;enabled++)
                {
                    cards.gameObject.SetActive(enabled==1);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                    pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                    System.IO.File.WriteAllBytes("Logs/CloudBankCards/"+(phase==0?"day-":"night-")+(angle==0?"gameplay-":angle==1?"lookdown-":"overview-")+(enabled==0?"off":"on")+".png",pixels.EncodeToPNG());
                }
            }
                System.IO.File.WriteAllText("Logs/CloudBankCards/qa.txt","utc="+System.DateTime.UtcNow.ToString("O")+"\nroots="+cards.transform.parent.GetComponentsInChildren<CloudBankCards>(true).Length+"\nrenderers="+cards.GetComponentsInChildren<MeshRenderer>(true).Length+"\ncolliders="+cards.GetComponentsInChildren<Collider>(true).Length+"\nGPU cost unmeasured.");
            }
            finally{camera.transform.SetPositionAndRotation(oldPosition,oldRotation);sky.SetTimeOfDayForQa(oldPhase);sky.SetLightingAuthorityForQa(oldAuthority);sky.EvaluatePresentationForQa();cards.gameObject.SetActive(active);camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
