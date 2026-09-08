using System;
using System.Collections;
using System.IO;
using Elemental.Input.Gestures;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthLandingSlamRuntimeTests
    {
        private Scene _prior, _scene;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (_prior.IsValid() && _prior.isLoaded) SceneManager.SetActiveScene(_prior);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
        [UnityTest] public IEnumerator ActualHighFallCutsAuthoredFloorEjectsMatterAndLaunchesOneRadialWave()
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
            var body = player.Body; var motor = player.GetComponent<PlanetMotor>();
            var input = player.GetComponent<MagicInputController>(); if (input != null) input.enabled = false;
            var slam = player.GetComponent<EarthLandingSlam>();
            if (slam == null) slam = player.gameObject.AddComponent<EarthLandingSlam>();
            Assert.That(input, Is.Not.Null); Assert.That(input.EarthExecutor, Is.Not.Null);
            slam.Configure(body, motor, input.EarthExecutor, player.GetComponent<EarthPillarWaveAbility>());
            Assert.That(slam.HasRequiredBindings, Is.True);
            deadline = Time.realtimeSinceStartup + 10;
            while (!motor.HasStableSupport && Time.realtimeSinceStartup < deadline) yield return new WaitForFixedUpdate();
            Assert.That(motor.HasStableSupport, Is.True);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Vector3 up = motor.LocalUp;
            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(body.position + up, -up, 4f, ~0, QueryTriggerInteraction.Ignore);
            EarthArenaStructure floor = null; Collider originalFloorCollider = null;
            foreach (var hit in hits)
            {
                var candidate = hit.collider.GetComponentInParent<EarthArenaStructure>();
                if (candidate != null && !candidate.OrdinaryDamageEnabled) { floor = candidate; originalFloorCollider = hit.collider; break; }
            }
            Assert.That(floor, Is.Not.Null, "Production proof must land on authored FloorBase, not only voxel SDF.");
            int piecesBefore = floor.ReleasedPieceCount;
            var planet = Find<VoxelPlanetBehaviour>(); int editsBefore = planet.State.EditCount;
            using var marker = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Bending.LandingSlam", 512);
            long peakNs = 0; var trace = new System.Text.StringBuilder();
            // A real upward velocity makes the observed support/flight episode;
            // no teleport, synthetic contact, or direct commit invocation is used.
            motor.BeginExternalLaunch(8); body.linearVelocity = up * 14f; slam.SetHeld(true);
            deadline = Time.realtimeSinceStartup + 12;
            while ((slam.CommitCount == 0 || slam.HasPendingTerrain) && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
                if (marker.Valid) peakNs = Math.Max(peakNs, marker.LastValue);
                trace.AppendLine($"height={Vector3.Distance(body.position,planet.transform.position):F3},speed={Vector3.Dot(body.linearVelocity,up):F2},grounded={motor.HasStableSupport},held={slam.IsHeld},observed={slam.HasObservedSupport},bindings={slam.HasRequiredBindings},enabled={slam.enabled},armed={slam.IsArmed},commits={slam.CommitCount},reject={slam.LastRejection}");
            }
            Directory.CreateDirectory("BuildReports/LandingSlam");
            File.WriteAllText("BuildReports/LandingSlam/physics-trace.txt", trace.ToString());
            Assert.That(slam.CommitCount, Is.EqualTo(1), slam.LastRejection);
            Assert.That(floor.ReleasedPieceCount, Is.GreaterThan(piecesBefore));
            Assert.That(floor.ReleasedPieceCount - piecesBefore, Is.LessThanOrEqualTo(4));
            Assert.That(floor.ReleasedPieceCount, Is.LessThan(floor.PieceCount), "A slam must not release the entire FloorBase.");
            Assert.That(slam.LastWaveColumnCount, Is.GreaterThan(8));
            Assert.That(slam.LastEjectedRockCount + slam.LastReleasedFloorPieces, Is.GreaterThanOrEqualTo(2));
            Assert.That(planet.State.EditCount, Is.GreaterThan(editsBefore));
            Assert.That(player.LastResponse, Is.Not.EqualTo(EarthCharacterImpactResponse.Knockout));
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            Assert.That(slam.CommitCount, Is.EqualTo(1), "Holding through the same support contact cannot repeat the slam.");
            // The intact broad floor collider must no longer bridge the hole.
            Assert.That(originalFloorCollider.enabled, Is.False, "The real originally hit FloorBase collider must be disabled by local fracture.");
            bool intactBridge = false;
            foreach (var collider in floor.GetComponents<Collider>())
                if (collider.enabled && collider.Raycast(new Ray(slam.LastImpactPoint + up * .5f, -up), out _, 1f)) intactBridge = true;
            Assert.That(intactBridge, Is.False);
            File.WriteAllText("BuildReports/LandingSlam/result.txt", $"UTC={DateTime.UtcNow:O}; floorPieces={slam.LastReleasedFloorPieces}; ejecta={slam.LastEjectedRockCount}; waveColumns={slam.LastWaveColumnCount}; peakMs={peakNs/1000000d:F3}");
        }
        private T Find<T>() where T : Component
        {
            var values = EarthArenaRoundSnapshot.SceneComponents<T>(_scene);
            Assert.That(values.Length, Is.GreaterThan(0), typeof(T).Name); return values[0];
        }
    }
}
