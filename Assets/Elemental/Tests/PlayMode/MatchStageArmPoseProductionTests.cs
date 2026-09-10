using System.Collections;
using System.Linq;
using Elemental.Presentation.UI;
using Elemental.Presentation.Rendering;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class MatchStageProductionTests
    {
        [UnityTest] public IEnumerator ResultArmsRemainOutsideTorsoAndWholeHatIsFramed()
        {
            previous=SceneManager.GetActiveScene();Assert.That(SceneManager.GetSceneByPath(Path).isLoaded,Is.False);
            yield return SceneManager.LoadSceneAsync(Path,LoadSceneMode.Additive);scene=SceneManager.GetSceneByPath(Path);SceneManager.SetActiveScene(scene);
            var flow=Find<FrontendFlowController>();var gate=Find<EarthSceneReadinessGate>();var stage=Find<MatchPresentationStage>();var hud=Find<EarthDuelHud>();
            double deadline=Time.realtimeSinceStartupAsDouble+145;
            while((!gate.IsReady||!stage.Ready)&&stage.Failure==null&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(stage.Failure,Is.Null);Assert.That(stage.Ready,Is.True);
            var camera=Find<CelestialSystemBehaviour>().TargetCamera;yield return captureResolution.WaitForRenderedSize(camera);
            for(int outcome=0;outcome<3;outcome++)
            {
                Assert.That(flow.BeginBot(),Is.True);deadline=Time.realtimeSinceStartupAsDouble+20;
                while(flow.State==FrontendState.Starting&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));
                hud.SetLocalPerspective(outcome==1?EarthDuelFighterId.Bot:EarthDuelFighterId.Player,true);
                flow.MatchController.ConfigureOnlineAuthority(false);
                Assert.That(flow.MatchController.ApplyReplicaMatch(100,100,4,outcome==2?4:2,0,true),Is.True);
                yield return new WaitForSecondsRealtime(1.1f);yield return new WaitForEndOfFrame();
                Assert.That(stage.ResultsActive,Is.True);
                Vector3 Bone(HumanBodyBones bone){Assert.That(stage.TryGetStageBonePosition(bone,out var p),Is.True);return p;}
                Vector3 leftShoulder=Bone(HumanBodyBones.LeftUpperArm),rightShoulder=Bone(HumanBodyBones.RightUpperArm);
                Vector3 lateral=(rightShoulder-leftShoulder).normalized,center=(leftShoulder+rightShoulder)*.5f;
                float torsoHalfWidth=Vector3.Distance(leftShoulder,rightShoulder)*.5f;
                Assert.That(Vector3.Dot(Bone(HumanBodyBones.RightHand)-center,lateral),Is.GreaterThan(torsoHalfWidth+.01f),"Right final wrist intersects the torso column.");
                Assert.That(-Vector3.Dot(Bone(HumanBodyBones.LeftHand)-center,lateral),Is.GreaterThan(torsoHalfWidth+.01f),"Left final wrist intersects the torso column.");
                var baked=new Mesh();
                try
                {
                    foreach(var renderer in stage.SubjectVisualRoot.GetComponentsInChildren<Renderer>().Where(s=>s.enabled&&s.gameObject.activeInHierarchy))
                    {
                        Mesh mesh;
                        if(renderer is SkinnedMeshRenderer skin){skin.BakeMesh(baked);mesh=baked;}
                        else{var filter=renderer.GetComponent<MeshFilter>();mesh=filter!=null?filter.sharedMesh:null;}
                        if(mesh==null)continue;
                        foreach(var vertex in mesh.vertices)
                        {
                            var view=camera.WorldToViewportPoint(renderer.transform.TransformPoint(vertex));
                            Assert.That(view.z,Is.GreaterThan(0));
                            Assert.That(view.y,Is.InRange(.015f,.985f),"Actual result skin/hat lies outside the final camera frame.");
                        }
                    }
                }
                finally{Object.Destroy(baked);}
                Capture("ArmPose-"+stage.Outcome);
                // Exercise the existing UI exit, then repeat a fresh result pose.
                var back=hud.GetComponent<UIDocument>().rootVisualElement.Q("round-result").Q<Button>("reference-result-menu");
                Assert.That(back.enabledInHierarchy,Is.True);
                using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=back.worldBound.center}))
                {down.target=back;back.SendEvent(down);}
                yield return null;
                using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=back.worldBound.center}))
                {up.target=back;back.SendEvent(up);}
                deadline=Time.realtimeSinceStartupAsDouble+5;
                while(flow.State!=FrontendState.Main&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(flow.State,Is.EqualTo(FrontendState.Main));
                flow.MatchController.ConfigureOnlineAuthority(true);
            }
        }
    }
}
