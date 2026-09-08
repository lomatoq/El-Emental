using System;
using System.IO;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.DistantScenery;
using Elemental.Runtime.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Elemental.Authoring.Editor
{
    public static class ValleyAtmosphereV2Setup
    {
        private static T Find<T>() where T:Component
        {
            T found=null;foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {var item=root.GetComponentInChildren<T>(true);if(item!=null){if(found!=null)throw new InvalidOperationException("Multiple "+typeof(T).Name+" owners in scene.");found=item;}}
            return found;
        }
        [MenuItem("Elemental/Graphics/Install Valley Atmosphere V2")]
        public static void Install()
        {
            var scene=SceneManager.GetActiveScene();if(Application.isPlaying || scene.name!="EarthCoreSlice")throw new InvalidOperationException("Open nonplaying EarthCoreSlice.");
            var planet=Find<VoxelPlanetBehaviour>();var backdrop=Find<DistantBackdrop>();
            if(planet==null || backdrop==null || backdrop.profile==null)throw new InvalidOperationException("Explicit planet and authored valley frame required.");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/Materials/AtmosphereFullscreen.mat");
            if(material==null || ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Existing atmosphere shader must import without errors.");
            const string path="Assets/Elemental/Content/Profiles/ValleyAtmosphereV2.asset";
            var profile=AssetDatabase.LoadAssetAtPath<ValleyAtmosphereProfile>(path);
            if(profile==null){profile=ScriptableObject.CreateInstance<ValleyAtmosphereProfile>();AssetDatabase.CreateAsset(profile,path);}
            profile.CloudArt=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Elemental/Content/Textures/Clouds/reference-bank-v1.png");
            if(profile.CloudArt==null)throw new InvalidOperationException("Import existing generated RGBA bank; no placeholder texture is allowed.");
            var existing=planet.transform.Find(ValleyAtmosphereController.OwnedName);
            if(existing!=null && existing.GetComponent<ValleyAtmosphereController>()==null)throw new InvalidOperationException("Frame name belongs to another owner.");
            var go=existing!=null?existing.gameObject:new GameObject(ValleyAtmosphereController.OwnedName);
            if(existing==null)Undo.RegisterCreatedObjectUndo(go,"Install optional valley atmosphere V2");
            var owner=go.GetComponent<ValleyAtmosphereController>();if(owner==null)owner=Undo.AddComponent<ValleyAtmosphereController>(go);
            owner.FogEnabled=true;owner.Configure(planet.transform,planet.WorldProfile!=null?planet.WorldProfile.Radius:planet.Radius,
                backdrop.stagingUp,backdrop.profile.heroViewDirection,profile);
            var rejected=Find<ValleyCloudStrata>();if(rejected!=null)owner.RetireRejectedVolume(rejected.gameObject);
            EditorUtility.SetDirty(owner);EditorUtility.SetDirty(profile);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            // No material defaults, vignette, renderer feature order, camera, UI, rock or light changes.
        }
        [MenuItem("Elemental/Graphics/Restore Original Atmosphere Owner")]
        public static void Restore()
        {
            var owner=Find<ValleyAtmosphereController>();if(owner==null)return;
            owner.RestoreOriginalOwner();EditorUtility.SetDirty(owner);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        [MenuItem("Elemental/Graphics/Capture Valley Atmosphere V2 Oracles")]
        public static void Capture()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Use a ready production Play session for QA.");
            var owner=Find<ValleyAtmosphereController>();var sky=Find<CelestialSystemBehaviour>();
            if(owner==null || sky==null || sky.TargetCamera==null)throw new InvalidOperationException("Install V2 and bind production sky/camera first.");
            var camera=sky.TargetCamera;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            bool oldFog=owner.FogEnabled,oldClouds=owner.CloudsEnabled,oldMotion=owner.AnimateClouds;int oldDebug=owner.DebugMode;
            float oldPhase=sky.Snapshot.TimeOfDay01;var oldAuthority=sky.LightingAuthority;
            var rt=new RenderTexture(1280,720,24);rt.Create();var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            string folder="Logs/ValleyAtmosphereV2";Directory.CreateDirectory(folder);
            try
            {
                owner.AnimateClouds=false;sky.SetLightingAuthorityForQa(Elemental.Simulation.Time.CelestialLightingAuthorityMode.AnimatedEphemeris);
                Vector3 centre=owner.transform.position,up=owner.transform.up,forward=owner.transform.forward;
                for(int phase=0;phase<2;phase++)for(int view=0;view<5;view++)
                {
                    camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                    if(view==1){camera.transform.position=centre+up*95+forward*125;camera.transform.rotation=Quaternion.LookRotation(centre-up*160-camera.transform.position,up);}
                    if(view==2){camera.transform.position=centre+up*140+forward*380;camera.transform.rotation=Quaternion.LookRotation(centre-up*40-camera.transform.position,up);}
                    if(view==3){camera.transform.position=centre-up*95+forward*250;camera.transform.rotation=Quaternion.LookRotation(forward-up*0.002f,up);}
                    if(view==4){camera.transform.position=centre+up*65+forward*180;camera.transform.rotation=Quaternion.LookRotation(forward-up*0.00001f,up);}
                    sky.SetTimeOfDayForQa(phase==0?0.25f:0.75f);sky.EvaluatePresentationForQa();
                    for(int mode=0;mode<4;mode++)
                    {
                        owner.FogEnabled=mode>0;owner.CloudsEnabled=mode==2;owner.DebugMode=mode==3?1:0;owner.Publish();
                        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                        string name=(phase==0?"day":"night")+"-"+new[]{"gameplay","lookdown","overview","under","shallow"}[view]+"-"+new[]{"original","veil","clouds","alpha"}[mode]+".png";
                        File.WriteAllBytes(Path.Combine(folder,name),pixels.EncodeToPNG());
                    }
                }
                File.WriteAllText(Path.Combine(folder,"qa.txt"),"utc="+DateTime.UtcNow.ToString("O")+"\nAnalytical alpha oracle preserves existing seismic/vignette/postprocess. GPU/CPU not measured. Camera, time, owner flags restored in finally.");
            }
            finally
            {
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
                owner.FogEnabled=oldFog;owner.CloudsEnabled=oldClouds;owner.AnimateClouds=oldMotion;owner.DebugMode=oldDebug;owner.Publish();
                sky.SetTimeOfDayForQa(oldPhase);sky.SetLightingAuthorityForQa(oldAuthority);sky.EvaluatePresentationForQa();
                Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);
            }
        }
    }
}
