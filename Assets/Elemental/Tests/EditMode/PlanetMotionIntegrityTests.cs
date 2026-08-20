using Elemental.Runtime.Diagnostics;
using Elemental.Simulation.Characters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Elemental.Tests.EditMode
{
    public sealed class PlanetMotionIntegrityTests
    {
        [Test]
        public void FullRagdoll_HasPriorityOverSupportAndCasting()
        {
            PlanetMotionState state = PlanetMotionIntegritySolver.ResolveState(
                true, false, true, false, 0f, 1f,
                CharacterPhysicalMode.FullRagdoll, true, true);

            Assert.That(state, Is.EqualTo(PlanetMotionState.FullRagdoll));
        }

        [Test]
        public void MovingSupport_IsExplicitAuthoritativeState()
        {
            PlanetMotionState state = PlanetMotionIntegritySolver.ResolveState(
                false, false, true, false, 0f, 0f,
                CharacterPhysicalMode.AnimatedMotor, false, false);

            Assert.That(state, Is.EqualTo(PlanetMotionState.SupportedMoving));
        }

        [Test]
        public void GroundedFrameWithoutContactOrSupport_IsFault()
        {
            var frame = new PlanetMotionFrame(
                8u,
                PlanetMotionState.GroundedStable,
                float3.zero,
                quaternion.identity,
                float3.zero,
                float3.zero,
                float2.zero,
                false,
                true,
                0,
                default);

            MotionFaultKind faults = PlanetMotionIntegritySolver.Evaluate(frame, 80f, 45f);

            Assert.That(faults & MotionFaultKind.GroundedWithoutContact, Is.Not.Zero);
        }

        [Test]
        public void Recorder_UsesBoundedRingAndCopiesChronologically()
        {
            var gameObject = new GameObject("MotionRecorderTest");
            EarthMotionReproRecorder recorder = gameObject.AddComponent<EarthMotionReproRecorder>();
            try
            {
                for (uint tick = 0; tick < 740u; tick++)
                {
                    var frame = new PlanetMotionFrame(
                        tick,
                        PlanetMotionState.AirborneFalling,
                        new float3(tick, 0f, 0f),
                        quaternion.identity,
                        float3.zero,
                        float3.zero,
                        float2.zero,
                        false,
                        false,
                        0,
                        default);
                    recorder.Record(frame, MotionFaultKind.None);
                }

                var frames = new PlanetMotionFrame[720];
                int count = recorder.CopyRecentFramesNonAlloc(frames);

                Assert.That(count, Is.EqualTo(720));
                Assert.That(frames[0].Tick, Is.EqualTo(20u));
                Assert.That(frames[719].Tick, Is.EqualTo(739u));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DeterministicLocomotionFuzz_ProducesNoImpossibleFiniteState()
        {
            uint random = 0xE17F0411u;
            for (uint tick = 0; tick < 100000u; tick++)
            {
                random = Next(random);
                bool grounded = (random & 1u) != 0u;
                bool movingSupport = grounded && (random & 2u) != 0u;
                bool edge = grounded && !movingSupport && (random & 4u) != 0u;
                bool jump = grounded && (random & 8u) != 0u;
                float vertical = ((random >> 8) & 255u) / 255f * 18f - 9f;
                float brace = ((random >> 16) & 255u) / 255f;
                PlanetMotionState state = PlanetMotionIntegritySolver.ResolveState(
                    grounded, edge, movingSupport, jump, vertical, brace,
                    CharacterPhysicalMode.AnimatedMotor, false, false);
                byte contacts = (byte)(grounded && !movingSupport ? 1 : 0);
                SupportFrameSnapshot support = movingSupport
                    ? new SupportFrameSnapshot(
                        42u, 1u, new float3(tick * 0.0001f, 0f, 0f), quaternion.identity,
                        new float3(0.1f, 0f, 0f), float3.zero, new float3(0.1f, 0f, 0f),
                        new float3(0f, 1f, 0f), false)
                    : default;
                var frame = new PlanetMotionFrame(
                    tick, state, new float3(tick * 0.0001f, 24f, 0f), quaternion.identity,
                    new float3(0f, vertical, 0f), float3.zero, float2.zero, jump,
                    grounded, contacts, in support);
                MotionFaultKind faults = PlanetMotionIntegritySolver.Evaluate(in frame, 80f, 45f);
                Assert.That(faults, Is.EqualTo(MotionFaultKind.None),
                    $"seed={random:X8} tick={tick} state={state}");
            }
        }

        private static uint Next(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }
    }
}
