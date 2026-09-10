using System.Collections;
using System.Collections.Generic;
using System.IO;
using Elemental.Presentation.UI;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Characters;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class GoldRespawnProductionTests
    {
        private const string ScenePath = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
        private const string Folder = "BuildReports/GoldRespawn";
        private Scene _scene, _previous;
        private EarthMvpDuelController _duel;
        private FrontendFlowController _flow;
        private GoldRespawnPresenter _gold;
        private bool _oldReduced;
        private GameObject _blocker;
        private ProductionCaptureResolution _capture;
        [System.Serializable] private sealed class Metrics
        { public int lifeCues, completed, cpuSamples, reservationFailures; public double cpuMeanNs; public long cpuPeakNs; }
        [UnitySetUp]
        public IEnumerator Load()
        {
            _capture=new ProductionCaptureResolution();
            _previous = SceneManager.GetActiveScene();
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(ScenePath); SceneManager.SetActiveScene(_scene);
            _flow = Find<FrontendFlowController>(); _duel = _flow.MatchController; _gold = Find<GoldRespawnPresenter>();
            Assert.That(_gold, Is.Not.Null, "Install Gold Respawn into the saved production scene before this test; the fixture does not install missing presentation.");
            var gate = Find<EarthSceneReadinessGate>(); double deadline = Time.realtimeSinceStartupAsDouble + 145;
            while ((!gate.IsReady || _flow.State == FrontendState.Loading || !_gold.StandingPosesReady) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(gate.IsReady && _gold.StandingPosesReady, Is.True);
            yield return _capture.WaitForRenderedSize(Find<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().TargetCamera);
            Assert.That(_gold.UniqueCueCount, Is.Zero, "Main/initial spawn must not produce a death cue.");
            _oldReduced = _flow.Preferences.ReducedMotion;
            Assert.That(_flow.BeginBot(), Is.True);
            while (_flow.State == FrontendState.Starting && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(_duel.CombatAllowed, Is.True); DisableBot();
            Directory.CreateDirectory(Folder);
        }
        [UnityTearDown]
        public IEnumerator Unload()
        {
            _capture?.Dispose();_capture=null;
            if (_blocker != null) Object.Destroy(_blocker);
            if (_flow != null) _flow.Preferences.Set(_flow.Preferences.MasterVolume, _flow.Preferences.UIVolume, _flow.Preferences.Sensitivity, _oldReduced);
            if (_previous.IsValid() && _previous.isLoaded) SceneManager.SetActiveScene(_previous);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
        [UnityTest]
        public IEnumerator TwentySimultaneousLifecyclesPreserveRootsMaterialsAndDeadline()
        {
            var actors = new[] { _duel.PlayerTransform, _duel.BotTransform };
            var scales = new[] { actors[0].localScale, actors[1].localScale };
            var rigScales = new[] { actors[0].GetComponentInChildren<HumanoidRagdollRig>(true).transform.localScale, actors[1].GetComponentInChildren<HumanoidRagdollRig>(true).transform.localScale };
            var capsules = new[] { actors[0].GetComponent<CapsuleCollider>(), actors[1].GetComponent<CapsuleCollider>() };
            var dimensions = new[] { new Vector2(capsules[0].radius, capsules[0].height), new Vector2(capsules[1].radius, capsules[1].height) };
            var materials = new Dictionary<Renderer, Material[]>();
            var colors = new Dictionary<Renderer, Color>(); var propertyBlock = new MaterialPropertyBlock();
            foreach (var actor in actors) foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                materials.Add(renderer, renderer.sharedMaterials); renderer.GetPropertyBlock(propertyBlock);
                colors.Add(renderer, propertyBlock.GetColor("_BaseColor"));
            }
            var generations = new uint[2]; var lastCue = new EarthRespawnCue[2];
            var completedAt = new double[2];
            int cueCount = 0, completed = 0;
            System.Action<EarthRespawnCue> onCue = cue =>
            {
                int index = (int)cue.Fighter;
                if (cue.LifeGeneration != generations[index])
                { Assert.That(cue.LifeGeneration, Is.GreaterThan(generations[index])); generations[index] = cue.LifeGeneration; cueCount++; }
                lastCue[index] = cue;
                Assert.That(cue.ActiveAt - cue.StartsAt, Is.LessThanOrEqualTo(.70001));
            };
            System.Action<EarthRespawnCue> onComplete = cue =>
            {
                int index = (int)cue.Fighter; completed++; completedAt[index] = _duel.RespawnPresentationTime;
                // Both physics and visible transform must be at the reservation when the proxy lease ends.
                Assert.That(Vector3.Distance(actors[index].GetComponent<Rigidbody>().position, new Vector3(cue.RootPosition.x, cue.RootPosition.y, cue.RootPosition.z)), Is.LessThan(.0001f), "Canonical physics spawn and reservation must be the same pose at the handoff boundary.");
                Assert.That(Vector3.Distance(actors[index].position, new Vector3(cue.RootPosition.x, cue.RootPosition.y, cue.RootPosition.z)), Is.LessThan(.0001f), "The released live renderer must not interpolate from the old death location.");
                Assert.That(Quaternion.Angle(actors[index].rotation, new Quaternion(cue.Rotation.value.x, cue.Rotation.value.y, cue.Rotation.value.z, cue.Rotation.value.w)), Is.LessThan(.01f));
                Assert.That(_duel.CanReceiveDamage(cue.Fighter), Is.True);
                foreach (var renderer in actors[index].GetComponentsInChildren<Renderer>(true))
                {
                    renderer.GetPropertyBlock(propertyBlock);
                    Assert.That(propertyBlock.GetColor("_BaseColor"), Is.EqualTo(colors[renderer]),
                        "The respawn handoff must restore the tint captured for this life before ordinary team presentation resumes.");
                }
            };
            _duel.RespawnCueChanged += onCue; _duel.RespawnCompleted += onComplete;
            long peak = 0; int samples = 0; double total = 0;
            using var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Respawn.GoldPresentation", 64);
            try
            {
                Assert.That(_gold.OwnedRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(_gold.OwnedRoot.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(_gold.OwnedRoot.GetComponentsInChildren<Animator>(true), Is.Empty);
                Assert.That(_gold.OwnedRoot.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                for (int cycle = 0; cycle < 20; cycle++)
                {
                    DisableBot();
                    _flow.Preferences.Set(_flow.Preferences.MasterVolume, _flow.Preferences.UIVolume, _flow.Preferences.Sensitivity, cycle >= 10);
                    foreach (var renderer in materials.Keys)
                    { renderer.GetPropertyBlock(propertyBlock); colors[renderer] = propertyBlock.GetColor("_BaseColor"); }
                    _duel.RequestKnockout(EarthDuelFighterId.Player, RagdollHandoff.Uniform(Vector3.zero));
                    _duel.RequestKnockout(EarthDuelFighterId.Bot, RagdollHandoff.Uniform(Vector3.zero));
                    Assert.That(_duel.PlayerPhase, Is.EqualTo(EarthDuelFighterPhase.KnockedOut));
                    Assert.That(_duel.BotPhase, Is.EqualTo(EarthDuelFighterPhase.KnockedOut));
                    double start = _duel.RespawnPresentationTime, deadline = Time.realtimeSinceStartupAsDouble + 7;
                    bool captured = false, capturedMiddle = false, capturedLate = false, paused = false, disabled = false;
                    while ((_duel.PlayerPhase != EarthDuelFighterPhase.Active || _duel.BotPhase != EarthDuelFighterPhase.Active) && Time.realtimeSinceStartupAsDouble < deadline)
                    {
                        yield return new WaitForEndOfFrame();
                        for (int actor = 0; actor < 2; actor++)
                        {
                            Assert.That(actors[actor].localScale, Is.EqualTo(scales[actor]));
                            Assert.That(new Vector2(capsules[actor].radius, capsules[actor].height), Is.EqualTo(dimensions[actor]));
                        }
                        if (cpu.LastValue > 0) { peak = System.Math.Max(peak, cpu.LastValue); total += cpu.LastValue; samples++; }
                        if (_gold.VisibleProxyCount > 0 && !captured)
                        {
                            captured = true;
                            if (cycle == 0 || cycle == 10) SaveCapture("Both-returning-" + cycle + ".png");
                        }
                        if ((cycle == 0 || cycle == 10) && lastCue[0].IsValid && _gold.VisibleProxyCount == 2)
                        {
                            double age = _duel.RespawnPresentationTime - lastCue[0].StartsAt;
                            if (!capturedMiddle && age >= .35) { capturedMiddle = true; SaveCapture("Both-returning-" + cycle + "-middle.png"); }
                            if (!capturedLate && age >= .62) { capturedLate = true; SaveCapture("Both-returning-" + cycle + "-late.png"); }
                        }
                        if (cycle == 2 && !paused && _gold.VisibleProxyCount == 2)
                        {
                            paused = true; _flow.Pause(); double held = _duel.RespawnPresentationTime;
                            yield return new WaitForSecondsRealtime(.25f);
                            Assert.That(_duel.RespawnPresentationTime, Is.EqualTo(held)); Assert.That(_gold.VisibleProxyCount, Is.EqualTo(2));
                            _flow.Resume(); DisableBot();
                        }
                        if (cycle == 3 && !disabled && _gold.VisibleProxyCount == 2)
                        {
                            disabled = true; _gold.enabled = false;
                            Assert.That(_gold.ActiveEffectCount, Is.Zero); Assert.That(_gold.VisibleProxyCount, Is.Zero);
                            yield return null; _gold.enabled = true;
                        }
                    }
                    Assert.That(_duel.PlayerPhase, Is.EqualTo(EarthDuelFighterPhase.Active)); Assert.That(_duel.BotPhase, Is.EqualTo(EarthDuelFighterPhase.Active));
                    Assert.That(completedAt[0] - start, Is.InRange(3.49, 3.54), "Visuals cannot add time to KO.");
                    Assert.That(completedAt[1] - start, Is.InRange(3.49, 3.54), "Visuals cannot add time to KO.");
                    Assert.That(_gold.ActiveEffectCount, Is.Zero); Assert.That(_gold.VisibleProxyCount, Is.Zero);
                    Assert.That(captured, Is.True); Assert.That(_duel.RespawnReservationFailureCount, Is.Zero);
                    foreach (var pair in materials)
                    {
                        CollectionAssert.AreEqual(pair.Value, pair.Key.sharedMaterials);
                        pair.Key.GetPropertyBlock(propertyBlock);
                        // The atomic completion callback checks this life's restored tint.
                        // Normal bot team/telegraph presentation may update it after the handoff.
                        Assert.That(propertyBlock.GetColor("_RespawnEmission"), Is.EqualTo(Color.clear), "Proxy gold emission must never leak onto canonical renderers.");
                    }
                    foreach (var actor in actors) Assert.That(actor.GetComponentInChildren<HumanoidRagdollRig>(true).IsRagdollActive, Is.False);
                    DisableBot();
                    // Sample the camera-visible frames after the fixed-tick callback, where
                    // old interpolation history previously escaped physics-only assertions.
                    for (int rendered = 0; rendered < 5; rendered++)
                    {
                        yield return new WaitForEndOfFrame();
                        for (int actor = 0; actor < actors.Length; actor++)
                        {
                            Rigidbody body = actors[actor].GetComponent<Rigidbody>();
                            float interpolationTravel = body.linearVelocity.magnitude * Time.fixedDeltaTime;
                            Assert.That(Vector3.Distance(actors[actor].position, body.position), Is.LessThan(.005f + interpolationTravel), "Rendered respawn may contain ordinary one-step interpolation, never the old death location.");
                            Assert.That(actors[actor].GetComponentInChildren<HumanoidRagdollRig>(true).transform.localScale, Is.EqualTo(rigScales[actor]), "The restored visible rig must retain the scale captured before the life cycle.");
                        }
                        Assert.That(_gold.VisibleProxyCount, Is.Zero, "Continuity cannot be achieved by concealing an already-active fighter behind a delayed proxy.");
                        var ups = Shader.GetGlobalVectorArray("_GoldRespawnWaveUps");
                        Assert.That(ups.Length, Is.EqualTo(2)); Assert.That(ups[0].w + ups[1].w, Is.Zero);
                        if (cycle == 0 || cycle == 10) SaveCapture("Both-active-" + cycle + "-frame-" + rendered + ".png");
                    }
                    yield return new WaitForSeconds(.15f);
                }
                Assert.That(cueCount, Is.EqualTo(40)); Assert.That(completed, Is.EqualTo(40)); Assert.That(_gold.UniqueCueCount, Is.EqualTo(40));
                File.WriteAllText(Folder + "/LifecycleMetrics.json", JsonUtility.ToJson(new Metrics {
                    lifeCues = cueCount, completed = completed, cpuSamples = samples, cpuMeanNs = samples > 0 ? total / samples : 0,
                    cpuPeakNs = peak, reservationFailures = _duel.RespawnReservationFailureCount }, true));
            }
            finally { _duel.RespawnCueChanged -= onCue; _duel.RespawnCompleted -= onComplete; }
        }
        [UnityTest]
        public IEnumerator LateBlockedReservationRevisesPoseAndRoundResetCancelsReveal()
        {
            _duel.RequestKnockout(EarthDuelFighterId.Player, RagdollHandoff.Uniform(Vector3.zero));
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            EarthRespawnCue before;
            while (!_duel.TryGetRespawnCue(EarthDuelFighterId.Player, out before) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(_duel.TryGetRespawnCue(EarthDuelFighterId.Player, out before), Is.True);
            _blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); _blocker.name = "Late spawn obstruction fixture";
            SceneManager.MoveGameObjectToScene(_blocker, _scene);
            _blocker.transform.SetPositionAndRotation(new Vector3(before.RootPosition.x, before.RootPosition.y, before.RootPosition.z),
                new Quaternion(before.Rotation.value.x, before.Rotation.value.y, before.Rotation.value.z, before.Rotation.value.w));
            UnityEngine.Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(_duel.TryGetRespawnCue(EarthDuelFighterId.Player, out var revised), Is.True);
            Assert.That(revised.LifeGeneration, Is.EqualTo(before.LifeGeneration)); Assert.That(revised.Revision, Is.GreaterThan(before.Revision));
            Assert.That(revised.ActiveAt, Is.EqualTo(before.ActiveAt)); Assert.That(revised.StartsAt, Is.GreaterThan(before.StartsAt));
            int cues = _gold.UniqueCueCount;
            _duel.RestartRound(); Assert.That(_gold.ActiveEffectCount, Is.Zero); Assert.That(_gold.VisibleProxyCount, Is.Zero);
            yield return null; Assert.That(_gold.UniqueCueCount, Is.EqualTo(cues), "Fresh round restoration must not create another death cue.");
            Assert.That(_duel.TryGetRespawnCue(EarthDuelFighterId.Player, out _), Is.False);
        }
        private void DisableBot() { var bot = _duel.BotTransform.GetComponent<EarthMvpBotController>(); if (bot != null) bot.enabled = false; }
        private static void SaveCapture(string name)
        {
            ProductionCaptureResolution.SaveScreen(Folder+"/"+name);
        }
        private T Find<T>() where T : Component
        { foreach (var root in _scene.GetRootGameObjects()) { var component = root.GetComponentInChildren<T>(true); if (component != null) return component; } return null; }
    }
}
