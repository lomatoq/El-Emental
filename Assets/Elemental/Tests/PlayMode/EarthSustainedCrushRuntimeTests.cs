using System.Collections;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Combat;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthSustainedCrushRuntimeTests
    {
        [UnityTest]
        public IEnumerator SleepingVerticalPileRetainsTransmittedUpperStoneWeight()
        {
            GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.transform.position = new Vector3(12000f, 0f, 0f);
            actor.transform.localScale = new Vector3(3f, .3f, 2f);
            Rigidbody body = actor.AddComponent<Rigidbody>();
            body.mass = 42f;
            body.isKinematic = true;
            var target = actor.AddComponent<EarthCharacterImpactTarget>();
            target.Configure(EarthDuelFighterId.Bot, 992u, body);
            GameObject[] stones = new GameObject[2];
            Rigidbody[] bodies = new Rigidbody[2];
            var fixedStep = new WaitForFixedUpdate();
            try
            {
                Assert.That(-UnityEngine.Physics.gravity.y, Is.GreaterThan(8f), "Fixture uses the project's existing downward PhysX gravity.");
                for (int i = 0; i < 2; i++)
                {
                    stones[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stones[i].transform.position = actor.transform.position + Vector3.up * (.67f + i * 1.02f);
                    bodies[i] = stones[i].AddComponent<Rigidbody>();
                    bodies[i].mass = 90f; // Own weight cap1260N is below crush threshold; actual stacked load exceeds it.
                    bodies[i].useGravity = true;
                    bodies[i].sleepThreshold = .005f;
                    stones[i].AddComponent<EarthArenaPiece>().Configure(null, i, new EarthPieceId((ushort)(i + 10)),
                        bodies[i], stones[i].GetComponent<Collider>(), null);
                }
                // Natural settling; do not call Sleep or inject any damage/load values.
                for (int frame = 0; frame < 240 && !(bodies[0].IsSleeping() && bodies[1].IsSleeping()); frame++)
                    yield return fixedStep;
                Assert.That(bodies[0].IsSleeping() && bodies[1].IsSleeping(), Is.True, "Both real stacked rocks must naturally sleep.");
                for (int frame = 0; frame < 35; frame++) yield return fixedStep;
                Assert.That(target.SustainedLoadNewtons, Is.GreaterThan(1400f), "Sleeping bottom stone must retain the upper stone's transmitted load.");
                Assert.That(target.IsUnderCrushingLoad, Is.True);
                // Removing the top wakes the supporting stone and measures the lighter load anew.
                stones[1].SetActive(false);
                for (int frame = 0; frame < 30; frame++) yield return fixedStep;
                Assert.That(target.IsUnderCrushingLoad, Is.False);
                Assert.That(target.SustainedLoadNewtons, Is.LessThan(1400f));
            }
            finally
            {
                foreach (GameObject stone in stones) if (stone != null) Object.DestroyImmediate(stone);
                Object.DestroyImmediate(actor);
            }
        }

        [UnityTest]
        public IEnumerator TwoRestingStonesTransmitCombinedLoadAndRemovalReleasesIt()
        {
            GameObject actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = "Crush contact fixture";
            actor.transform.position = new Vector3(10000f, 0f, 0f);
            actor.transform.localScale = new Vector3(3f, .3f, 2f);
            Rigidbody body = actor.AddComponent<Rigidbody>();
            body.mass = 42f;
            body.isKinematic = true;
            var target = actor.AddComponent<EarthCharacterImpactTarget>();
            target.Configure(EarthDuelFighterId.Bot, 991u, body);
            GameObject[] stones = new GameObject[2];
            try
            {
                for (int i = 0; i < stones.Length; i++)
                {
                    stones[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stones[i].transform.position = actor.transform.position + new Vector3(i == 0 ? -.65f : .65f, .7f, 0f);
                    Rigidbody stoneBody = stones[i].AddComponent<Rigidbody>();
                    stoneBody.mass = 60f;
                    stoneBody.useGravity = false;
                    stoneBody.sleepThreshold = 0f;
                    stones[i].AddComponent<EarthArenaPiece>().Configure(null, i, new EarthPieceId((ushort)(i + 1)),
                        stoneBody, stones[i].GetComponent<Collider>(), null);
                }
                for (int frame = 0; frame < 55; frame++)
                {
                    foreach (GameObject stone in stones)
                        stone.GetComponent<Rigidbody>().AddForce(Vector3.down * 14f, ForceMode.Acceleration);
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(target.SustainedLoadNewtons, Is.GreaterThan(1400f), "Both real support contacts must contribute.");
                Assert.That(target.IsUnderCrushingLoad, Is.True);
                foreach (GameObject stone in stones) stone.SetActive(false);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.That(target.IsUnderCrushingLoad, Is.False);
                Assert.That(target.SustainedLoadNewtons, Is.Zero);
            }
            finally
            {
                foreach (GameObject stone in stones) if (stone != null) Object.DestroyImmediate(stone);
                Object.DestroyImmediate(actor);
            }
        }
    }
}
