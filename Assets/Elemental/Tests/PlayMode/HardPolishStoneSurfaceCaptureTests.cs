#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace Elemental.Tests.PlayMode
{
    public sealed class HardPolishStoneSurfaceCaptureTests
    {
        [UnityTest,Timeout(300000)]
        public IEnumerator ActualSavedStoneSurfaceSamePoseBeforeAfter()
        {
            const string path="Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            const string output="BuildReports/HardPolish/G02/StoneSurface";
            Directory.CreateDirectory(output);
            Scene prior=SceneManager.GetActiveScene(),scene=default;
            float oldClock=Time.timeScale;
            var originals=new Dictionary<Renderer,Material[]>();
            var clones=new Dictionary<Material,Material>();
            var resolution=new ProductionCaptureResolution();
            FireVisualCaptureCamera cameraOwner=null;
            try
            {
                Assert.That(SceneManager.GetSceneByPath(path).isLoaded,Is.False);
                yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByPath(path);SceneManager.SetActiveScene(scene);
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<FrontendFlowController>(true)).Single();
                double until=Time.realtimeSinceStartupAsDouble+150;
                while(!flow.IsWorldReady&&Time.realtimeSinceStartupAsDouble<until)yield return null;
                Assert.That(flow.BeginBot(),Is.True);until=Time.realtimeSinceStartupAsDouble+60;
                while(flow.State!=FrontendState.Combat&&Time.realtimeSinceStartupAsDouble<until)yield return null;
                Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
                Camera camera=roots.SelectMany(r=>r.GetComponentsInChildren<CelestialSystemBehaviour>(true)).Single().TargetCamera;
                yield return resolution.WaitForRenderedSize(camera);
                var renderers=roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).ToArray();
                foreach(var renderer in renderers)
                {
                    Material[] materials=renderer.sharedMaterials;
                    bool changed=false;
                    for(int i=0;i<materials.Length;i++)
                    {
                        Material source=materials[i];
                        if(source==null||!source.HasProperty("_StoneWeathering")||source.GetFloat("_SurfaceMode")>.5f)continue;
                        if(!changed){originals.Add(renderer,renderer.sharedMaterials);changed=true;}
                        if(!clones.TryGetValue(source,out Material candidate))
                        {candidate=new Material(source){name=source.name+" surface QA"};clones.Add(source,candidate);}
                        materials[i]=candidate;
                    }
                    if(changed)renderer.sharedMaterials=materials;
                }
                Assert.That(clones.Count,Is.GreaterThan(0));
                Vector3 at=camera.transform.position,focus=flow.MatchController.PlayerTransform.position+Vector3.up;
                cameraOwner=camera.gameObject.AddComponent<FireVisualCaptureCamera>();
                Time.timeScale=0;
                foreach(int view in new[]{0,1,2})
                {
                    Vector3 position=view==0?at:focus+Quaternion.AngleAxis(view==1?28:-28,Vector3.up)*(at-focus)*.78f;
                    cameraOwner.Place(position,focus);
                    foreach(string variant in new[]{"before","matte","weathered-matte"})
                    {
                        foreach(var pair in clones)
                        {
                            Material material=pair.Value;
                            string sourcePath=UnityEditor.AssetDatabase.GetAssetPath(pair.Key);
                            bool hadRadialNormals=sourcePath.EndsWith("/RumbleBasalt.mat")||sourcePath.EndsWith("/RumbleClay.mat")||
                                sourcePath.EndsWith("/RumbleGround.mat")||sourcePath.EndsWith("/RumbleLimestone.mat")||
                                sourcePath.EndsWith("/RumbleSandstone.mat")||sourcePath.EndsWith("/LooseEarthChipVfx.mat");
                            material.SetFloat("_SideShadingSmoothness",variant=="before"&&hadRadialNormals?1:0);
                            material.SetFloat("_StoneWeathering",variant=="weathered-matte"?.72f:0);
                            material.SetFloat("_StoneMatte",variant=="before"?0:1);
                        }
                        for(int warm=0;warm<8;warm++)yield return null;
                        yield return new WaitForEndOfFrame();
                        ProductionCaptureResolution.SaveScreen(Path.Combine(output,$"view-{view}-{variant}.png"));
                    }
                }
                File.WriteAllText(Path.Combine(output,"scope.txt"),
                    "Actual saved scene/camera/lighting; cloned stone materials compare prior radial-normal/specular response, geometry normals plus matte, and geometry normals plus weathered-matte (.72 weathering). Prior radial normals are restored only on the six materials changed by the correction. Character materials excluded. Three same-pose native triplets. Visual judgement required; no beauty acceptance inferred from this test. Source materials are never saved.");
            }
            finally
            {
                Time.timeScale=oldClock;
                if(cameraOwner!=null)Object.Destroy(cameraOwner);
                foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
                foreach(Material material in clones.Values)Object.Destroy(material);
                resolution.Dispose();
                if(prior.IsValid()&&prior.isLoaded)SceneManager.SetActiveScene(prior);
                if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
