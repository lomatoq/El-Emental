using System;
using System.IO;
using UnityEngine.Rendering.Universal;
using Elemental.Presentation.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Elemental.Authoring.Editor
{
    public static class ValleyAtmosphereParticleSetup
    {
        private static ValleyAtmosphereController Find()
        {
            ValleyAtmosphereController owner=null;
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {var item=root.GetComponentInChildren<ValleyAtmosphereController>(true);if(item==null)continue;if(owner!=null)throw new InvalidOperationException("Multiple atmosphere owners.");owner=item;}
            return owner;
        }
        [MenuItem("Elemental/Graphics/Install Valley Image Particle Clouds")]
        public static void Install()
        {
            if(Application.isPlaying || SceneManager.GetActiveScene().name!="EarthCoreSlice")throw new InvalidOperationException("Use nonplaying EarthCoreSlice.");
            var owner=Find();if(owner==null)throw new InvalidOperationException("Install V2 first.");
            var shader=Shader.Find("Elemental/Valley Image Cloud Particles");
            if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Particle shader must compile first.");
            var art=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Elemental/Content/Textures/Clouds/reference-bank-v1.png");
            if(art==null)throw new InvalidOperationException("Existing authored transparent cloud bank required.");
            const string path="Assets/Elemental/Content/Materials/ValleyImageCloudParticles.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_BaseMap",art);material.SetFloat("_Opacity",0.78f);EditorUtility.SetDirty(material);
            var old=owner.transform.Find(ValleyCloudParticles.OwnedName);
            if(old!=null && old.GetComponent<ValleyCloudParticles>()==null)throw new InvalidOperationException("Cloud child is owned by another component.");
            var go=old!=null?old.gameObject:new GameObject(ValleyCloudParticles.OwnedName);
            if(old==null)Undo.RegisterCreatedObjectUndo(go,"Install bounded image particle clouds");
            var particles=go.GetComponent<ValleyCloudParticles>();if(particles==null)particles=Undo.AddComponent<ValleyCloudParticles>(go);
            particles.Configure(owner,material);go.SetActive(true);
            Undo.RecordObject(owner,"Select image particle cloud backend");owner.UseParticleClouds=true;owner.CloudsEnabled=true;owner.Publish();
            EditorUtility.SetDirty(owner);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        [MenuItem("Elemental/Graphics/Capture Valley Sun Rays On Off")]
        public static void CaptureRays()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Use ready production Play.");
            CelestialSystemBehaviour sky=null;
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())sky=root.GetComponentInChildren<CelestialSystemBehaviour>(true)??sky;
            if(sky==null || sky.TargetCamera==null)throw new InvalidOperationException("Explicit production sky/camera required.");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/Materials/AtmosphereFullscreen.mat");
            var camera=sky.TargetCamera;float strength=material.GetFloat("_SunDustStrength"),phase=sky.Snapshot.TimeOfDay01;
            var authority=sky.LightingAuthority;var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            var data=camera.GetComponent<UniversalAdditionalCameraData>();bool dithering=data!=null && data.dithering;
            var rt=new RenderTexture(1920,1080,24);rt.Create();var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            const string folder="Logs/ValleySunRays";Directory.CreateDirectory(folder);double[] differences=new double[2];
            try
            {
                if(data!=null)data.dithering=false;
                sky.SetLightingAuthorityForQa(Elemental.Simulation.Time.CelestialLightingAuthorityMode.AnimatedEphemeris);
                for(int day=0;day<2;day++)
                {
                    sky.SetTimeOfDayForQa(day==0?0.25f:0.75f);sky.EvaluatePresentationForQa();Color32[] off=null;
                    for(int mode=0;mode<2;mode++)
                    {
                        material.SetFloat("_SunDustStrength",mode==0?0:strength);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                        pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
                        File.WriteAllBytes(folder+"/"+(day==0?"day":"night")+(mode==0?"-off.png":"-on.png"),pixels.EncodeToPNG());
                        var current=pixels.GetPixels32();if(mode==0)off=current;
                        else{double sum=0;for(int i=0;i<current.Length;i++)sum+=Math.Abs(current[i].r-off[i].r)+Math.Abs(current[i].g-off[i].g)+Math.Abs(current[i].b-off[i].b);differences[day]=sum/(current.Length*3*255);}
                    }
                }
                File.WriteAllText(folder+"/evidence.txt","utc="+DateTime.UtcNow.ToString("O")+"\nstrength="+strength+"\ndayMeanAbs="+differences[0].ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\nnightMeanAbs="+differences[1].ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\nOriginal strength/gating reused; no isolated GPU claim. Dithering disabled only for subtraction, restored.");
            }
            finally
            {
                material.SetFloat("_SunDustStrength",strength);camera.targetTexture=previousTarget;RenderTexture.active=previousActive;if(data!=null)data.dithering=dithering;
                sky.SetTimeOfDayForQa(phase);sky.SetLightingAuthorityForQa(authority);sky.EvaluatePresentationForQa();
                UnityEngine.Object.DestroyImmediate(pixels);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        [MenuItem("Elemental/Graphics/Restore Valley Analytical Cloud Banks")]
        public static void Restore()
        {
            var owner=Find();if(owner==null)return;Undo.RecordObject(owner,"Restore analytical cloud banks");owner.UseParticleClouds=false;owner.Publish();
            var child=owner.transform.Find(ValleyCloudParticles.OwnedName);if(child!=null)child.gameObject.SetActive(false);
            EditorUtility.SetDirty(owner);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
