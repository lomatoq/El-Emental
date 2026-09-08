using System;
using System.Collections;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthLandingSlamPlanetRuntimeTests
    {
        private Scene _prior, _scene;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (_prior.IsValid() && _prior.isLoaded) SceneManager.SetActiveScene(_prior);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
        [UnityTest] public IEnumerator ActualPlanetLandingCutsColliderAndEjectsFourReservedRocks()
        {
            _prior = SceneManager.GetActiveScene();
            const string path = "Assets/Elemental/Content/Scenes/EarthCoreSlice.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByPath(path); SceneManager.SetActiveScene(_scene);
            var gate = Find<EarthSceneReadinessGate>();
            float deadline = Time.realtimeSinceStartup + 130;
            while (!gate.IsReady && !gate.Failed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(gate.IsReady, Is.True, gate.Status);
            yield return ProductionCombatTestFlow.BeginBotAfterReadiness(_scene);
            foreach (var bot in EarthArenaRoundSnapshot.SceneComponents<EarthMvpBotController>(_scene)) bot.enabled = false;
            EarthCharacterImpactTarget player = null;
            foreach (var target in EarthArenaRoundSnapshot.SceneComponents<EarthCharacterImpactTarget>(_scene))
                if (target.FighterId == EarthDuelFighterId.Player) { player = target; break; }
            Assert.That(player, Is.Not.Null);
            var planet = Find<VoxelPlanetBehaviour>(); var body = player.Body;
            var motor = player.GetComponent<PlanetMotor>();
            var input = player.GetComponent<MagicInputController>();
            Assert.That(input, Is.Not.Null); var executor = input.EarthExecutor;
            Assert.That(executor, Is.Not.Null);
            var slam = player.GetComponent<EarthLandingSlam>();
            if (slam == null) slam = player.gameObject.AddComponent<EarthLandingSlam>();
            slam.Configure(body, motor, executor, player.GetComponent<EarthPillarWaveAbility>());
            Vector3 originalUp = motor.LocalUp;
            Vector3 axis = Vector3.Cross(originalUp, Mathf.Abs(originalUp.y) < .9f ? Vector3.up : Vector3.right).normalized;
            RaycastHit terrainHit = default; bool found = false;
            for (int candidate = 0; candidate < 8 && !found; candidate++)
            {
                Vector3 direction = Quaternion.AngleAxis(70 + candidate * 25, axis) * originalUp;
                var hits = UnityEngine.Physics.RaycastAll(planet.transform.position + direction * (planet.State.Radius + 25f),
                    -direction, 55f, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity; RaycastHit closest = default;
                foreach (var hit in hits) if (hit.distance < nearest) { nearest = hit.distance; closest = hit; }
                if (closest.collider == null || closest.collider.GetComponentInParent<VoxelPlanetBehaviour>() != planet ||
                    Vector3.Dot(closest.normal, direction) < .8f) continue;
                terrainHit = closest; found = true;
            }
            Assert.That(found, Is.True, "Find actual exposed planet terrain away from the authored arena.");
            Assert.That(terrainHit.collider.GetComponentInParent<EarthArenaStructure>(), Is.Null);
            Vector3 up = (terrainHit.point - planet.transform.position).normalized;
            float supportOffset = Mathf.Max(.2f, Vector3.Dot(body.position - motor.SupportFeetPoint(originalUp), originalUp));
            Quaternion rotation = Quaternion.FromToRotation(body.rotation * Vector3.up, up) * body.rotation;
            player.GetComponent<ActiveRagdollPuppet>().ResetPhysicalState(terrainHit.point + up * (supportOffset + .15f), rotation);
            player.GetComponentInChildren<HumanoidRagdollRig>(true)?.ResetToAnimated();
            motor.ResetAfterTeleport(); slam.Cancel();
            input.enabled = false;
            UnityEngine.Physics.SyncTransforms();
            deadline = Time.realtimeSinceStartup + 12;
            while (!motor.HasStableSupport && Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
            Assert.That(motor.HasStableSupport, Is.True);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Vector3 probe = terrainHit.point - up * .25f;
            float originalDensity = planet.State.SampleDensityMaterial((float3)planet.transform.InverseTransformPoint(probe)).Density;
            Assert.That(originalDensity, Is.LessThanOrEqualTo(0f), "The shared before/after sample starts inside real solid.");
            int edits = planet.State.EditCount;
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Bending.LandingSlam", 512);
            long peakNs = 0;
            // Relocation above was setup followed by acquired stable support. The
            // tested fall is generated physically from that real planet surface.
            motor.BeginExternalLaunch(8); body.linearVelocity = up * 14f; slam.SetHeld(true);
            deadline = Time.realtimeSinceStartup + 12;
            var trace = new System.Text.StringBuilder();
            while ((slam.CommitCount == 0 || slam.HasPendingTerrain) && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                if (recorder.Valid) peakNs = Math.Max(peakNs, recorder.LastValue);
                trace.AppendLine($"height={Vector3.Distance(body.position,planet.transform.position):F3},speed={Vector3.Dot(body.linearVelocity,up):F2},grounded={motor.HasStableSupport},held={slam.IsHeld},observed={slam.HasObservedSupport},bindings={slam.HasRequiredBindings},armed={slam.IsArmed},count={slam.CommitCount},reject={slam.LastRejection}");
            }
            Directory.CreateDirectory("BuildReports/LandingSlam");
            File.WriteAllText("BuildReports/LandingSlam/planet-trace.txt", trace.ToString());
            Assert.That(slam.CommitCount, Is.EqualTo(1), slam.LastRejection);
            Assert.That(slam.LastReleasedFloorPieces, Is.Zero, "This proof uses no authored arena floor.");
            Assert.That(slam.LastEjectedRockCount, Is.EqualTo(4));
            Assert.That(slam.LastWaveColumnCount, Is.EqualTo(36));
            Assert.That(planet.State.EditCount, Is.GreaterThan(edits));
            Assert.That(slam.HasPendingTerrain, Is.False);
            Assert.That(Vector3.Distance(slam.LastImpactPoint, terrainHit.point), Is.LessThan(.9f));
            float carvedDensity = planet.State.SampleDensityMaterial((float3)planet.transform.InverseTransformPoint(probe)).Density;
            Assert.That(carvedDensity, Is.GreaterThan(0f), "The same solid point must become air in canonical SDF.");
            float colliderDepth = float.NaN;
            foreach (var hit in UnityEngine.Physics.RaycastAll(terrainHit.point + up * 2f, -up, 7f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<VoxelPlanetBehaviour>() == planet)
                {
                    float depth = Vector3.Dot(terrainHit.point - hit.point, up);
                    if (float.IsNaN(colliderDepth) || depth < colliderDepth) colliderDepth = depth;
                }
            Assert.That(float.IsNaN(colliderDepth), Is.False, "The committed crater still has a real physical bottom.");
            Assert.That(colliderDepth, Is.GreaterThan(.5f), "Mesh collider must expose the carved hole rather than bridge the old surface.");
            Assert.That(player.LastResponse, Is.Not.EqualTo(EarthCharacterImpactResponse.Knockout));
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
            Assert.That(slam.CommitCount, Is.EqualTo(1));
            File.WriteAllText("BuildReports/LandingSlam/planet-result.txt", $"UTC={DateTime.UtcNow:O}; originalDensity={originalDensity:F4}; carvedDensity={carvedDensity:F4}; colliderDepth={colliderDepth:F3}; ejecta={slam.LastEjectedRockCount}; wave={slam.LastWaveColumnCount}; peakMs={peakNs/1000000d:F3}");
        }
        private T Find<T>() where T : Component
        {
            var values = EarthArenaRoundSnapshot.SceneComponents<T>(_scene);
            Assert.That(values.Length, Is.GreaterThan(0), typeof(T).Name); return values[0];
        }
    }
}
