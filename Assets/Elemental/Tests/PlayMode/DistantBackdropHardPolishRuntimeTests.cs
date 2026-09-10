#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.DistantScenery;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Elemental.Tests.PlayMode
{
    public sealed class DistantBackdropHardPolishRuntimeTests
    {
        private Scene scene,previous;
        private ProductionCaptureResolution captureResolution;
        [UnityTest]
        public IEnumerator ProductionOrbitPreservesAuthoredGeographyAndCapturesEightBearings()
        {
            captureResolution=new ProductionCaptureResolution();
            previous=SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("Assets/Elemental/Content/Scenes/EarthCoreSlice.unity",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneAt(SceneManager.sceneCount-1);SceneManager.SetActiveScene(scene);
            var gate=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EarthSceneReadinessGate>(true)).Single();
            double deadline=Time.realtimeSinceStartupAsDouble+140;
            while(!gate.IsReady&&!gate.Failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);
            var backdrop=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DistantBackdrop>(true)).Single();
            Assert.That(backdrop.profile.hardPolishSupplements,Is.True,"Apply reviewed G09 profile and rebake/rebuild before acceptance capture.");
            backdrop.ApplyTime(73);backdrop.environmentPaused=true;
            Transform[] transforms=backdrop.GetComponentsInChildren<Transform>();
            Matrix4x4[] matrices=transforms.Select(t=>t.localToWorldMatrix).ToArray();
            var sky=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Elemental.Presentation.Rendering.CelestialSystemBehaviour>(true)).Single();
            var camera=sky.TargetCamera;Assert.That(camera,Is.Not.Null);
            yield return captureResolution.WaitForRenderedSize(camera);
            string folder="BuildReports/HardPolish/G09/Orbit";Directory.CreateDirectory(folder);
            Vector3 up=backdrop.stagingUp.normalized;
            Vector3 forward=Vector3.ProjectOnPlane(backdrop.profile.heroViewDirection,up).normalized;
            Vector3 arena=backdrop.planetCenter.position+up*backdrop.profile.planetRadius;
            for(int bearing=0;bearing<8;bearing++)
            {
                Vector3 direction=Quaternion.AngleAxis(bearing*45,up)*forward;
                yield return null;
                Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
                float oldFov=camera.fieldOfView,oldFar=camera.farClipPlane;
                camera.fieldOfView=58;camera.farClipPlane=Mathf.Max(oldFar,3000);
                camera.transform.SetPositionAndRotation(arena+up*18-direction*35,Quaternion.LookRotation(direction+up*.12f,up));
                var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
                var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
                RenderTexture active=RenderTexture.active;
                try
                {
                    var request=new RenderPipeline.StandardRequest{destination=target};
                    Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
                    RenderPipeline.SubmitRenderRequest(camera,request);
                    RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply(false,false);
                    File.WriteAllBytes(folder+"/bearing-"+(bearing*45).ToString("000")+".png",texture.EncodeToPNG());
                }
                finally{camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.fieldOfView=oldFov;camera.farClipPlane=oldFar;RenderTexture.active=active;Object.Destroy(texture);target.Release();Object.Destroy(target);}
                for(int i=0;i<transforms.Length;i++)Assert.That(transforms[i].localToWorldMatrix,Is.EqualTo(matrices[i]));
            }
            Assert.That(backdrop.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(backdrop.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            captureResolution?.Dispose();captureResolution=null;
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
