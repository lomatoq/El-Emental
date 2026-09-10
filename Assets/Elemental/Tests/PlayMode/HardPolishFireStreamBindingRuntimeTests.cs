using System;
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Animation;
using Elemental.Presentation.Fire;
using Elemental.Presentation.UI;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Fire;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        private Scene scene,previous;
        private float previousTimeScale;
        private FireStreamPresentationBinding binding;
        private FrontendFlowController flow;
        private EarthMvpDuelController duel;
        [Serializable] private sealed class Report
        {
            public string utc,backend;public int presenters,beginCount,particlesBeforeRelease,particlesAfterRelease,coherentTriangles;
            public long beginManagedBytes;public float bodyToMuzzle;
            public bool mainBlocked,startingCancelled,pauseStops,deathStops,resetStops,priorityStops,drained,noAutoResume;
        }
        private readonly Report report=new Report();
        [UnitySetUp] public IEnumerator Load()
        {
            previous=SceneManager.GetActiveScene();previousTimeScale=Time.timeScale;
            yield return SceneManager.LoadSceneAsync("Assets/Elemental/Content/Scenes/EarthCoreSlice.unity",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneAt(SceneManager.sceneCount-1);SceneManager.SetActiveScene(scene);
            var gate=All<EarthSceneReadinessGate>().Single();flow=All<FrontendFlowController>().Single();duel=flow.MatchController;
            binding=All<FireStreamPresentationBinding>().Single();
            double deadline=Time.realtimeSinceStartupAsDouble+140;
            while((!gate.IsReady||!binding.IsReady||flow.State==FrontendState.Loading)&&!gate.Failed&&binding.Failure==null&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(gate.IsReady,Is.True,gate.Status);Assert.That(binding.IsReady,Is.True,binding.Failure);
            Assert.That(flow.State,Is.EqualTo(FrontendState.Main));
            report.utc=DateTime.UtcNow.ToString("O");report.presenters=binding.PresenterCount;
        }
        [UnityTest,Timeout(240000)]
        public IEnumerator LocalDuelStreamPrewarmsTracksHandDrainsAndStopsAtLifecycleBoundaries()
        {
            FireStreamSession session=binding.PlayerSession;
            Assert.That(binding.PresenterCount,Is.EqualTo(4).Or.EqualTo(8));
            for(int i=0;i<binding.PresenterCount;i++)
            {Assert.That(binding.Presenter(i).IsReady,Is.True);Assert.That(binding.Presenter(i).ActiveBackend,Is.EqualTo(FireVisualBackendSelection.CpuMesh));}
            Assert.That(session.TryBegin(Vector3.up*10),Is.False);report.mainBlocked=true;
            Assert.That(flow.BeginBot(),Is.True);yield return null;
            Assert.That(session.IsAvailable,Is.False);flow.EndMatch();yield return null;
            Assert.That(session.IsActive,Is.False);report.startingCancelled=true;
            yield return EnterCombat();
            var actor=duel.PlayerTransform.GetComponentInChildren<HumanoidCharacterPresentation>();
            Transform hand=actor.Animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.That(binding.PlayerMuzzle.parent,Is.EqualTo(hand));
            Vector3 focus=session.MuzzlePosition+duel.PlayerTransform.up*8;
            Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.Reset();
            int begins=binding.PoseBegins;long bytes=GC.GetAllocatedBytesForCurrentThread();
            bool admitted=session.TryBegin(focus);report.beginManagedBytes=Elemental.Runtime.Diagnostics.HardPolishAllocationCounters.IsSupported?GC.GetAllocatedBytesForCurrentThread()-bytes:-1;
            Assert.That(admitted,Is.True);
            uint generation=session.Generation;int slot=session.Group.Slot;
            for(int i=0;i<45;i++)
            {
                session.SetAim(session.MuzzlePosition+duel.PlayerTransform.up*8);
                yield return null;
                Assert.That(session.Generation,Is.EqualTo(generation));
                Assert.That(binding.PoseBegins,Is.EqualTo(begins+1),"A target update began a second pose session.");
            }
            var view=binding.Presenter(slot);report.backend=view.ActiveBackend.ToString();
            if(view.CpuDiagnostics.FlowDiagnostics!=null)
            {
                var gas=view.CpuDiagnostics.FlowDiagnostics;
                Assert.That(gas.Visible,Is.True);
                Assert.That(gas.Solver.Count,Is.GreaterThan(0));
                report.coherentTriangles=gas.ProxyTriangles;
                float nearest=float.PositiveInfinity;
                for(int i=0;i<gas.Solver.Count;i++)nearest=Mathf.Min(nearest,Vector3.Distance((Vector3)gas.Solver.Particles[i].Position,session.MuzzlePosition));
                report.bodyToMuzzle=nearest;
                Assert.That(nearest,Is.LessThan(.5f),"Transported gas must inject at the actual hand nozzle.");
            }
            else
            {
                Assert.That(view.CpuDiagnostics.BodyDiagnostics,Is.Not.Null);
                Assert.That(view.CpuDiagnostics.BodyDiagnostics.Visible,Is.True);
                report.coherentTriangles=view.CpuDiagnostics.BodyDiagnostics.ActiveTriangles;
                Assert.That(report.coherentTriangles,Is.GreaterThan(0));
                Assert.That(view.CpuDiagnostics.BodyDiagnostics.TryGetCenterlinePoint(0,0,out Vector3 bodyStart),Is.True);
                report.bodyToMuzzle=Vector3.Distance(bodyStart,session.MuzzlePosition);
                Assert.That(report.bodyToMuzzle,Is.LessThan(.5f));
            }
            report.particlesBeforeRelease=view.AliveParticles;
            session.Stop();yield return null;
            report.particlesAfterRelease=view.AliveParticles;
            Assert.That(binding.DrainingViews,Is.GreaterThan(0));
            Assert.That(view.AliveParticles,Is.GreaterThan(0),"Release cleared the tail before canonical retirement.");
            Assert.That(actor.PoseController.FireChannelPresentationActive,Is.False);
            yield return WaitSeconds(1.1f);
            Assert.That(binding.DrainingViews,Is.Zero);report.drained=true;
            Assert.That(session.IsActive,Is.False);
            Assert.That(session.TryBegin(session.MuzzlePosition+duel.PlayerTransform.up*8),Is.True);
            flow.Pause();yield return null;
            Assert.That(session.IsActive,Is.False);report.pauseStops=true;
            flow.Resume();yield return null;Assert.That(session.IsActive,Is.False);report.noAutoResume=true;
            Assert.That(session.TryBegin(session.MuzzlePosition+duel.PlayerTransform.up*8),Is.True);
            actor.PoseController.SetPresentationSuppressed(true);yield return null;
            Assert.That(session.IsActive,Is.False);report.priorityStops=true;
            actor.PoseController.SetPresentationSuppressed(false);yield return null;
            Assert.That(session.IsActive,Is.False);
            Assert.That(session.TryBegin(session.MuzzlePosition+duel.PlayerTransform.up*8),Is.True);
            duel.RestartRound();yield return null;
            Assert.That(session.IsActive,Is.False);report.resetStops=true;
            yield return WaitCombatReady();
            Assert.That(session.TryBegin(session.MuzzlePosition+duel.PlayerTransform.up*8),Is.True);
            var handoff=RagdollHandoff.Uniform(Vector3.zero);
            Assert.That(duel.ApplyDamage(EarthDuelFighterId.Player,duel.MaximumHealth+1,in handoff),Is.True);
            yield return null;Assert.That(session.IsActive,Is.False);report.deathStops=true;
            report.beginCount=binding.PoseBegins;
        }
        [UnityTest,Timeout(240000)]
        public IEnumerator BridgeShutdownEndsBothPosesClearsViewsAndReenableDoesNotRestart()
        {
            yield return EnterCombat();
            var localActor=duel.PlayerTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            var botActor=duel.BotTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            var local=binding.PlayerSession;var rival=binding.BotSession;
            Assert.That(local.TryBegin(local.MuzzlePosition+duel.PlayerTransform.up*8),Is.True);
            yield return WaitSeconds(.2f);
            int slot=local.Group.Slot;
            local.Stop();yield return null;
            Assert.That(binding.DrainingViews,Is.GreaterThan(0),"Ordinary Stop preserves the existing tail.");
            Assert.That(binding.Presenter(slot).AliveParticles,Is.GreaterThan(0));
            yield return WaitSeconds(1.1f);
            Assert.That(local.TryBegin(local.MuzzlePosition+duel.PlayerTransform.up*8),Is.True);
            Assert.That(rival.TryBegin(rival.MuzzlePosition+duel.BotTransform.up*8),Is.True);
            yield return WaitSeconds(.2f);
            int ends=0;local.Ended+=_=>ends++;rival.Ended+=_=>ends++;
            binding.enabled=false;
            Assert.That(ends,Is.EqualTo(2),"Shutdown must retain ordinary Ended pose callbacks.");
            Assert.That(local.IsActive||rival.IsActive,Is.False);
            Assert.That(local.IsAvailable||rival.IsAvailable,Is.False);
            Assert.That(localActor.PoseController.FireChannelPresentationActive,Is.False);
            Assert.That(botActor.PoseController.FireChannelPresentationActive,Is.False);
            Assert.That(binding.ActiveViews,Is.Zero);Assert.That(binding.DrainingViews,Is.Zero);
            for(int i=0;i<binding.PresenterCount;i++)Assert.That(binding.Presenter(i).AliveParticles,Is.Zero);
            yield return WaitSeconds(.3f);
            var localInput=duel.PlayerTransform.GetComponentsInChildren<Elemental.Input.Gestures.MagicInputController>(true).Single();
            var audio=localInput.EarthExecutor.GetComponent<Elemental.Presentation.VFX.EarthAudioDirector>();
            Assert.That(audio.ActiveFireLoops,Is.Zero);
            binding.enabled=true;yield return WaitSeconds(.2f);
            Assert.That(local.IsActive||rival.IsActive,Is.False,"Re-enabling the bridge cannot replay a hold.");
            Assert.That(ends,Is.EqualTo(2));
            yield return WaitSeconds(1.1f);
            Assert.That(local.TryBegin(local.MuzzlePosition+duel.PlayerTransform.up*8),Is.True);
            local.Stop();yield return null;Assert.That(binding.DrainingViews,Is.GreaterThan(0));
        }
        [UnityTest,Timeout(240000)]
        public IEnumerator CosmeticAbToggleKeepsAdmittedFireGameplayAndPoseAlive()
        {
            yield return EnterCombat();
            var session=binding.PlayerSession;
            var actor=duel.PlayerTransform.GetComponentsInChildren<HumanoidCharacterPresentation>(true).Single();
            Assert.That(session.TryBegin(session.MuzzlePosition+duel.PlayerTransform.up*8),Is.True);
            yield return WaitSeconds(.2f);uint generation=session.Generation;var group=session.Group;
            binding.SetCosmeticRenderingForQa(false);
            Assert.That(session.IsActive&&session.IsAvailable,Is.True);
            Assert.That(actor.PoseController.FireChannelPresentationActive,Is.True);
            Assert.That(binding.ActiveViews,Is.Zero);Assert.That(binding.DrainingViews,Is.Zero);
            yield return WaitSeconds(.2f);
            Assert.That(session.Generation,Is.EqualTo(generation));Assert.That(session.Group,Is.EqualTo(group));
            Assert.That(session.CurrentLength,Is.GreaterThan(0));
            binding.SetCosmeticRenderingForQa(true);yield return null;
            Assert.That(session.IsActive,Is.True);Assert.That(binding.ActiveViews,Is.GreaterThan(0));
            Assert.That(session.Generation,Is.EqualTo(generation));
            session.Stop();yield return null;Assert.That(binding.DrainingViews,Is.GreaterThan(0));
        }
        private IEnumerator EnterCombat()
        {
            double deadline=Time.realtimeSinceStartupAsDouble+140;
            while(!flow.IsWorldReady&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.BeginBot(),Is.True);yield return WaitCombatReady();
        }
        private IEnumerator WaitCombatReady()
        {
            double deadline=Time.realtimeSinceStartupAsDouble+140;
            while((flow.State!=FrontendState.Combat||!binding.PlayerSession.IsAvailable)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(flow.State,Is.EqualTo(FrontendState.Combat));Assert.That(binding.PlayerSession.IsAvailable,Is.True);
            foreach(var bot in All<EarthMvpBotController>())bot.enabled=false;
        }
        private static IEnumerator WaitSeconds(float seconds)
        {float elapsed=0;while(elapsed<seconds){elapsed+=Time.deltaTime;yield return null;}}
        private T[] All<T>()where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Directory.CreateDirectory("BuildReports/HardPolish/G05");File.WriteAllText("BuildReports/HardPolish/G05/PresentationBinding.json",JsonUtility.ToJson(report,true));
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale=previousTimeScale;
        }
    }
}
