using Elemental.Presentation.Rendering;
using Elemental.Runtime.World;
using Elemental.Presentation.DistantScenery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elemental.Authoring.Editor
{
    public static class ValleyCloudStrataSetup
    {
        [MenuItem("Elemental/Graphics/Capture Valley Cloud QA")]
        public static void CaptureQa()
        {
            if(!Application.isPlaying)throw new System.InvalidOperationException("Cloud capture requires a ready EarthCoreSlice Play session.");
            var scene=SceneManager.GetActiveScene();ValleyCloudStrata cloud=null;CelestialSystemBehaviour sky=null;DistantBackdrop backdrop=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                cloud=root.GetComponentInChildren<ValleyCloudStrata>(true)??cloud;
                sky=root.GetComponentInChildren<CelestialSystemBehaviour>(true)??sky;
                backdrop=root.GetComponentInChildren<DistantBackdrop>(true)??backdrop;
            }
            if(cloud==null || sky==null || backdrop==null || sky.TargetCamera==null)throw new System.InvalidOperationException("Missing cloud/sky/production camera/backdrop.");
            var camera=sky.TargetCamera;var view=cloud.GetComponent<MeshRenderer>();
            var urp=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();bool oldPost=urp!=null && urp.renderPostProcessing;
            var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldTarget=camera.targetTexture;
            var bokeh=camera.GetComponent<MiniBokeh.MiniBokehController>();bool oldBokeh=bokeh!=null && bokeh.enabled;
            var oldActive=RenderTexture.active;var oldEnabled=view.enabled;float oldPhase=sky.Snapshot.TimeOfDay01;var oldAuthority=sky.LightingAuthority;
            string folder="Logs/ValleyClouds/NoPostQa";System.IO.Directory.CreateDirectory(folder);
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);rt.Create();
            var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                if(urp!=null)urp.renderPostProcessing=false;if(bokeh!=null)bokeh.enabled=false;
                sky.SetLightingAuthorityForQa(Elemental.Simulation.Time.CelestialLightingAuthorityMode.AnimatedEphemeris);
                Vector3 up=backdrop.stagingUp.normalized;Vector3 tangent=Vector3.ProjectOnPlane(oldRotation*Vector3.forward,up).normalized;
                if(tangent.sqrMagnitude<0.5f)tangent=Vector3.Cross(up,Vector3.right).normalized;
                for(int phase=0;phase<2;phase++)for(int angle=0;angle<3;angle++)
                {
                    sky.SetTimeOfDayForQa(phase==0?0.25f:0.75f);sky.EvaluatePresentationForQa();
                    camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                    if(angle>0)
                    {
                        Vector3 centre=cloud.transform.parent.position;
                        camera.transform.position=centre+up*(angle==1?95:140)+tangent*(angle==1?125:380);
                        camera.transform.rotation=Quaternion.LookRotation(centre-up*(angle==1?160:40)-camera.transform.position,up);
                    }
                    for(int enabled=0;enabled<2;enabled++)
                    {
                        view.enabled=enabled==1;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                        string shot=(phase==0?"day":"night")+"-"+(angle==0?"gameplay":angle==1?"lookdown":"overview")+"-"+(enabled==0?"off":"on")+".png";
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,shot),pixels.EncodeToPNG());
                    }
                }
                var scale=cloud.transform.localScale;var centreLocal=cloud.transform.localPosition;
                int colliders=cloud.GetComponentsInChildren<Collider>(true).Length;
                System.IO.File.WriteAllText(folder+"/qa.txt","utc="+System.DateTime.UtcNow.ToString("O")+"\nclouds="+cloud.transform.parent.GetComponentsInChildren<ValleyCloudStrata>(true).Length+"\ncolliders="+colliders+"\nscale="+scale+"\nlocalCentre="+centreLocal+"\nGPU timing not measured. QA temporarily changes camera pose and time, restoring both; no scene save.");
            }
            finally
            {
                if(urp!=null)urp.renderPostProcessing=oldPost;if(bokeh!=null)bokeh.enabled=oldBokeh;
                view.enabled=oldEnabled;camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
                camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                sky.SetTimeOfDayForQa(oldPhase);sky.SetLightingAuthorityForQa(oldAuthority);sky.EvaluatePresentationForQa();
                Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);
            }
        }
        [MenuItem("Elemental/Graphics/Install Valley Cloud Strata")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Leave Play before installing cloud strata.");
            var scene=SceneManager.GetActiveScene();
            if(scene.name!="EarthCoreSlice")throw new System.InvalidOperationException("Open EarthCoreSlice; installer only owns its Valley Cloud Strata child.");
            VoxelPlanetBehaviour planet=null;DistantBackdrop backdrop=null;
            foreach(var root in scene.GetRootGameObjects())
            {
                var authoredBackdrop=root.GetComponentInChildren<DistantBackdrop>(true);
                if(authoredBackdrop!=null)backdrop=authoredBackdrop;
                var candidate=root.GetComponentInChildren<VoxelPlanetBehaviour>(true);
                if(candidate==null)continue;
                if(planet!=null)throw new System.InvalidOperationException("Multiple planets: select a single authored arena scene.");
                planet=candidate;
            }
            if(backdrop==null)throw new System.InvalidOperationException("Cloud strata requires the authored distant valley staging-up frame.");
            if(planet==null)throw new System.InvalidOperationException("No authored voxel planet in active scene.");
            var shader=Shader.Find("Elemental/Valley Cloud Strata");
            var noise=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Elemental/Content/Textures/CloudNoise64.asset");
            if(shader==null || ShaderUtil.ShaderHasError(shader) || noise==null)throw new System.InvalidOperationException("Import cloud shader and existing CloudNoise64 first.");
            const string path="Assets/Elemental/Content/Materials/ValleyCloudStrata.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_CloudNoise",noise);material.SetFloat("_Coverage",0.70f);material.SetFloat("_Density",0.024f);EditorUtility.SetDirty(material);
            var child=planet.transform.Find(ValleyCloudStrata.OwnedName);
            if(child!=null && child.GetComponent<ValleyCloudStrata>()==null)throw new System.InvalidOperationException("Cloud name already belongs to another object; refusing overwrite.");
            var go=child!=null?child.gameObject:new GameObject(ValleyCloudStrata.OwnedName);
            if(child==null)Undo.RegisterCreatedObjectUndo(go,"Install valley cloud strata");
            var layer=go.GetComponent<ValleyCloudStrata>();if(layer==null)layer=Undo.AddComponent<ValleyCloudStrata>(go);
            var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            float radius=planet.WorldProfile!=null?planet.WorldProfile.Radius:planet.Radius;
            layer.Configure(planet.transform,radius,backdrop.stagingUp,cube,material);
            EditorUtility.SetDirty(layer);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            Debug.Log("Valley clouds installed. Scene left dirty for review; no camera, fog, arena or physics changes.");
        }
    }
}

