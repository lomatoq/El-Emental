using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using Elemental.Input.Gestures;
using Elemental.Input.Actions;
using Elemental.Presentation.Fire;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Bending;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class HardPolishResponseProductionTests
    {
        private Scene scene, previous;
        private float oldScale;
        private DirectFireInputLease directInputLease;
        private T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        [UnityTest, Timeout(240000)]
        public IEnumerator ExistingResponseOwnersKeepSimultaneousFireDistinctAndFloodBounded()
        {
            previous = SceneManager.GetActiveScene(); oldScale = Time.timeScale;
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(scene);
            var flow = All<FrontendFlowController>().Single(); var duel = flow.MatchController;
            var binding = All<FireStreamPresentationBinding>().Single(); var gate = All<EarthSceneReadinessGate>().Single();
            var hub = All<EarthMaterialFeedbackHub>().Single();
            var feedback = All<EarthMagicFeedback>().Single();
            var input = duel.PlayerTransform.GetComponentsInChildren<MagicInputController>(true).Single();
            var audio = input.EarthExecutor.GetComponent<EarthAudioDirector>(); Assert.That(audio, Is.Not.Null);
            double deadline = Time.realtimeSinceStartupAsDouble + 145;
            while ((!gate.IsReady || !binding.IsReady || flow.State == FrontendState.Loading) && !gate.Failed && binding.Failure == null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status); Assert.That(binding.IsReady, Is.True, binding.Failure);
            Assert.That(flow.BeginBot(), Is.True);
            while (flow.State != FrontendState.Combat && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(flow.State, Is.EqualTo(FrontendState.Combat)); yield return null;
            var bot = duel.BotTransform.GetComponent<EarthMvpBotController>(); if (bot != null) bot.enabled = false;
            int audioBefore = audio.ResponseAudioEvents, sparksBefore = feedback.PolishSparkEvents;
            int ignitionCues = 0, endingCues = 0, switchCues = 0;
            var trace=new List<string>();
            int droppedBefore=hub.DroppedEvents,mergedBefore=hub.CoalescedEvents;
            binding.PlayerSession.Began+=(generation,group)=>trace.Add($"player begin token={generation} frame={Time.frameCount}");
            binding.BotSession.Began+=(generation,group)=>trace.Add($"bot begin token={generation} frame={Time.frameCount}");
            binding.PlayerSession.Ended+=generation=>trace.Add($"player ended token={generation} frame={Time.frameCount} stack={System.Environment.StackTrace}");
            binding.BotSession.Ended+=generation=>trace.Add($"bot ended token={generation} frame={Time.frameCount} stack={System.Environment.StackTrace}");
            hub.Presented += cue => {
                trace.Add($"present {cue.Kind} source={cue.SourceId} generation={cue.Generation} frame={Time.frameCount}");
                if (cue.Kind == EarthMaterialFeedbackKind.FireIgnite) ignitionCues++;
                if (cue.Kind == EarthMaterialFeedbackKind.FireEnd) endingCues++;
                if (cue.Kind == EarthMaterialFeedbackKind.SchoolSwitch) switchCues++;
            };
            Assert.That(input.TrySelectElement(ElementId.Fire), Is.True); yield return null;
            Assert.That(switchCues, Is.EqualTo(1), "Install explicit response bindings into the saved production scene.");
            var router=input.GetComponent<EarthActionRouterBehaviour>();Assert.That(router,Is.Not.Null);
            // ActiveRagdollPuppet reasserts its control list every FixedUpdate. Lease ingress
            // out of that list while retaining its real physical/support simulation.
            directInputLease=new DirectFireInputLease(input);
            yield return new WaitForFixedUpdate();
            trace.Add($"fixture ingress lease input={input.GetEntityId()} enabled={input.enabled} router={router.GetEntityId()} enabled={router.enabled} combat={duel.CombatAllowed} frame={Time.frameCount}");
            Assert.That(input.isActiveAndEnabled,Is.False);Assert.That(router.isActiveAndEnabled,Is.False);
            Assert.That(binding.PlayerSession.TryBegin(binding.PlayerSession.MuzzlePosition + duel.PlayerTransform.up * 8), Is.True);
            Assert.That(binding.BotSession.TryBegin(binding.BotSession.MuzzlePosition + duel.BotTransform.up * 8), Is.True);
            // Explicit session calls are used here to test presentation fan-out independently of the input fixture.
            yield return null;
            // Begin is confirmed in Feedback.Update after all admission callbacks;
            // the hub publishes in LateUpdate(800), after a yield-null continuation.
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory("BuildReports/PolishResponses");
            object Field(string name)=>typeof(EarthMagicFeedback).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(feedback);
            trace.Add($"ingress after publish input={input.GetEntityId()} enabled={input.enabled} router={router.GetEntityId()} enabled={router.enabled} combat={duel.CombatAllowed}");
            trace.Add($"after publish playerActive={binding.PlayerSession.IsActive} botActive={binding.BotSession.IsActive} localPending={Field("pendingLocalIgnition")} botPending={Field("pendingBotIgnition")} localConfirmed={Field("localIgnited")} botConfirmed={Field("botIgnited")} dropped={hub.DroppedEvents-droppedBefore} merged={hub.CoalescedEvents-mergedBefore}");
            File.WriteAllLines("BuildReports/PolishResponses/AdmissionTrace.txt",trace);
            Assert.That(binding.PlayerSession.IsActive&&binding.BotSession.IsActive,Is.True,"Both direct sessions must survive before presentation fan-out is counted; see AdmissionTrace for any Stop caller.");
            Assert.That(ignitionCues, Is.EqualTo(2), "Both admitted generations must publish after the response hub LateUpdate.");
            Assert.That(binding.PlayerSession.IsActive&&binding.BotSession.IsActive,Is.True);
            Assert.That(audio.ActiveFireLoops, Is.EqualTo(2), "Both live authority sessions must own distinct playing loop sources.");
            Directory.CreateDirectory("BuildReports/PolishResponses");
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes("BuildReports/PolishResponses/Simultaneous-Fire.png", image.EncodeToPNG()); Object.Destroy(image);
            binding.PlayerSession.Stop(); binding.BotSession.Stop(); yield return null;
            // Stop was issued after LateUpdate; observe the next real hub publication.
            yield return new WaitForEndOfFrame();
            File.WriteAllLines("BuildReports/PolishResponses/AdmissionTrace.txt",trace);
            Assert.That(endingCues, Is.EqualTo(2));
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(audio.ActiveFireLoops, Is.Zero);
            Assert.That(audio.ResponseAudioEvents - audioBefore, Is.EqualTo(5));
            Assert.That(feedback.PolishSparkEvents - sparksBefore, Is.GreaterThanOrEqualTo(3));
            int sourceCount = audio.GetComponentsInChildren<AudioSource>(true).Length;
            int presented = hub.PresentedEvents, sounds = audio.ResponseAudioEvents;
            for (uint i = 0; i < 200; i++) hub.Emit(EarthMaterialFeedbackKind.SchoolSwitch,
                duel.PlayerTransform.position, duel.PlayerTransform.up, .5f, .08f, 100 + i, i, 0, 0, ElementId.Fire);
            hub.FlushPending();
            Assert.That(hub.PresentedEvents - presented, Is.LessThanOrEqualTo(8));
            Assert.That(audio.ResponseAudioEvents - sounds, Is.LessThanOrEqualTo(4));
            Assert.That(audio.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sourceCount));
            Assert.That(Time.timeScale, Is.EqualTo(1), "Cosmetic feedback must not introduce hitstop.");
            audio.enabled = false; feedback.enabled = false; yield return null;
            Assert.That(audio.ActiveFireLoops, Is.Zero);
            File.WriteAllText("BuildReports/PolishResponses/Budget.txt",
                $"two actor ignitions={ignitionCues}; endings={endingCues}; school={switchCues}; fixed audio sources={sourceCount}; flood presented={hub.PresentedEvents-presented}; flood audio={audio.ResponseAudioEvents-sounds}");
        }
        [UnityTearDown] public IEnumerator Unload()
        {
            directInputLease?.Dispose();directInputLease=null;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = oldScale;
        }
    }
}
