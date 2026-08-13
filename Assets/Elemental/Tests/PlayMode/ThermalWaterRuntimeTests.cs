using System.Collections;
using Elemental.Presentation.VFX;
using Elemental.Runtime.Physics;
using Elemental.Runtime.World;
using Elemental.Simulation.Materials;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class ThermalWaterRuntimeTests
    {
        [UnityTest]
        public IEnumerator EightOperatorsRemainBoundedAndConservative()
        {
            GameObject root = new GameObject("Thermal Water Operator Test");
            root.SetActive(false);
            ThermalWaterWorldBehaviour world = root.AddComponent<ThermalWaterWorldBehaviour>();
            world.Configure(16, 8, 8, 10f, 8);
            ThermalWaterMagicExecutor executor = root.AddComponent<ThermalWaterMagicExecutor>();
            executor.Configure(world);
            MaterialDefinition water = MaterialDefinition.Water;
            WaterVolume a = new WaterVolume(
                new WaterVolumeId(1), 1u, float3.zero, float3.zero, 1f,
                new PhaseState(water.Id, PhaseKind.Liquid, 20f, 2f));
            WaterVolume b = new WaterVolume(
                new WaterVolumeId(2), 1u, new float3(2f, 0f, 0f), float3.zero, 1f,
                new PhaseState(water.Id, PhaseKind.Liquid, 20f, 1f));
            world.Water.Register(in a);
            world.Water.Register(in b);
            root.SetActive(true);

            Assert.That(executor.ApplyWaterOperator(WaterOperatorKind.TransferMass, 0, 0.5f, float3.zero, 1u, 1), Is.True);
            Assert.That(executor.ApplyWaterOperator(WaterOperatorKind.AddHeat, 0, 50f, float3.zero, 2u), Is.True);
            Assert.That(executor.ApplyWaterOperator(WaterOperatorKind.RemoveHeat, 0, 50f, float3.zero, 3u), Is.True);
            Assert.That(executor.ApplyWaterOperator(WaterOperatorKind.Freeze, 0, 0f, float3.zero, 4u), Is.True);
            Assert.That(world.Water.GetVolume(0).State.Phase, Is.EqualTo(PhaseKind.Solid));
            Assert.That(executor.ApplyWaterOperator(WaterOperatorKind.Melt, 0, 0f, float3.zero, 5u), Is.True);
            Assert.That(world.Water.GetVolume(0).State.Phase, Is.EqualTo(PhaseKind.Liquid));
            Assert.That(executor.ApplyWaterOperator(WaterOperatorKind.Vaporize, 0, 0f, float3.zero, 6u), Is.True);
            Assert.That(world.Water.GetVolume(0).State.Phase, Is.EqualTo(PhaseKind.Gas));
            Assert.That(executor.ApplyWaterOperator(WaterOperatorKind.Condense, 0, 0f, float3.zero, 7u), Is.True);
            Assert.That(world.Water.GetVolume(0).State.Phase, Is.EqualTo(PhaseKind.Liquid));
            Assert.That(executor.ApplyWaterOperator(
                WaterOperatorKind.ApplyPressureImpulse, 0, 1000f, new float3(1f, 0f, 0f), 8u), Is.True);

            WaterVolume result = world.Water.GetVolume(0);
            Assert.That(math.length(result.Velocity), Is.LessThanOrEqualTo(40.01f));
            Assert.That(world.Water.Telemetry.CurrentMass, Is.EqualTo(3f).Within(0.001f));
            Assert.That(world.Water.Telemetry.MassError, Is.EqualTo(0f).Within(0.001f));
            Assert.That(world.Water.Telemetry.EnergyError, Is.EqualTo(0f).Within(0.001f));
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ElementLabCrossElementReplayKeepsCanonicalStateFinite()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Assets/Elemental/Content/Scenes/ElementLab.unity",
                LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            ElementLabDriver driver = Object.FindAnyObjectByType<ElementLabDriver>();
            ThermalWaterWorldBehaviour world = Object.FindAnyObjectByType<ThermalWaterWorldBehaviour>();
            ThermalWaterMagicExecutor executor = Object.FindAnyObjectByType<ThermalWaterMagicExecutor>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(executor, Is.Not.Null);
            for (int tick = 0; tick < 30; tick++) yield return new WaitForFixedUpdate();

            Assert.That(driver.ScriptedCommandCount, Is.EqualTo(6));
            Assert.That(driver.ReactionCount, Is.EqualTo(1));
            Assert.That(executor.Recorder.Count, Is.EqualTo(6));
            Assert.That(world.Water.Count, Is.EqualTo(3));
            Assert.That(world.Water.Telemetry.CurrentMass, Is.EqualTo(14f).Within(0.001f));
            Assert.That(world.Water.Telemetry.MassError, Is.EqualTo(0f).Within(0.001f));
            Assert.That(world.Water.Telemetry.EnergyError, Is.EqualTo(0f).Within(0.001f));
            for (int index = 0; index < world.Water.Count; index++)
            {
                WaterVolume volume = world.Water.GetVolume(index);
                Assert.That(float.IsFinite(volume.State.Temperature), Is.True);
                Assert.That(math.all(math.isfinite(volume.Center)), Is.True);
                Assert.That(math.all(math.isfinite(volume.Velocity)), Is.True);
            }
            WaterVolumeVisualProxy[] visuals = Object.FindObjectsByType<WaterVolumeVisualProxy>();
            Assert.That(visuals.Length, Is.GreaterThanOrEqualTo(3));
            ReactionImpulseBody impactBody = Object.FindAnyObjectByType<ReactionImpulseBody>();
            Assert.That(impactBody, Is.Not.Null);
            Assert.That(impactBody.AppliedReactionCount, Is.GreaterThanOrEqualTo(1));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(
                SceneManager.GetSceneByPath("Assets/Elemental/Content/Scenes/ElementLab.unity"));
            if (unload != null) yield return unload;
        }
    }
}
