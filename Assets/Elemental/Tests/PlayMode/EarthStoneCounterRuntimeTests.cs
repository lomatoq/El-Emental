using System.Collections;
using System.Linq;
using Elemental.Runtime.Characters;
using Elemental.Runtime.Matter;
using Elemental.Runtime.Physics;
using Elemental.Simulation.Matter;
using Elemental.Simulation.Structures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthStoneCounterRuntimeTests
    {
        [UnityTest]
        public IEnumerator RealPartitionsConserveMassAndGuardChildrenDoNotCounterAgain()
        {
            var host = new GameObject("Counter test kernel");
            var kernel = host.AddComponent<EarthMatterKernelBehaviour>();
            var poolObject = new GameObject("Counter test debris"); poolObject.SetActive(false);
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube); template.SetActive(false);
            var pool = poolObject.AddComponent<EarthRockDebrisPool>();
            pool.Configure(24, null, template.GetComponent<MeshFilter>().sharedMesh, null, null);
            pool.ConfigureMatterKernel(kernel); poolObject.SetActive(true);
            var defender = new GameObject("Counter defender"); defender.transform.position = new Vector3(0,1000,0);
            var body = defender.AddComponent<Rigidbody>(); body.useGravity = false; body.mass = 75;
            var guard = defender.AddComponent<EarthStoneCounterGuard>(); guard.Configure(body,null,pool);
            GameObject source = null;
            try
            {
                yield return null;
                float[] radii = { .2f, .7f, 1.6f };
                int[] counts = { 0, 2, 3 };
                for (int index = 0; index < radii.Length; index++)
                {
                    guard.SetHeld(false,Vector3.forward);
                    foreach(var old in pool.GetComponentsInChildren<EarthRockDebris>(false)) old.gameObject.SetActive(false);
                    body.position = new Vector3(index*30,1000,0); body.linearVelocity = Vector3.zero;
                    source = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    source.AddComponent<Rigidbody>().useGravity = false;
                    var identity = source.AddComponent<EarthMatterIdentity>();
                    var fragment = source.AddComponent<EarthFragment>();
                    fragment.Initialize((uint)(index+100),null,body.position+Vector3.forward*1.6f,radii[index],96);
                    pool.PrepareFracture(source.GetComponent<Collider>(), counts[index] == 0 ? 2 : counts[index]);
                    fragment.Body.linearVelocity = Vector3.back*12;
                    Physics.SyncTransforms();
                    guard.SetHeld(true,Vector3.forward);
                    uint sequence = guard.Sequence;
                    Assert.That(guard.TickGuard(.02f),Is.True,guard.LastRejection);
                    Assert.That(guard.Sequence,Is.EqualTo(sequence+1));
                    Assert.That(source.activeSelf,Is.False);
                    var children = pool.GetComponentsInChildren<EarthRockDebris>(false);
                    Assert.That(children.Length,Is.EqualTo(counts[index]));
                    if(index==0)
                    {
                        Assert.That(identity.IsDormantProxyReleased,Is.True);
                        var records=new EarthMatterRecord[64];
                        int recordCount=kernel.Registry.CopyActiveNonAlloc(records);
                        var archived=records.Take(recordCount).Single(r=>r.Representation==EarthRepresentationTier.DormantRecord);
                        Assert.That(archived.Mass,Is.EqualTo(96).Within(.001f));
                    }
                    else
                    {
                        Assert.That(children.Sum(x=>x.EarthMass),Is.EqualTo(96).Within(.001f));
                        foreach(var child in children)
                        {
                            Assert.That(child.CounterGuardEligible,Is.False);
                            Assert.That(Vector3.Dot(child.Body.linearVelocity,Vector3.forward),Is.GreaterThanOrEqualTo(-.001f));
                            child.Body.position=body.position+Vector3.forward*1.5f;
                            child.Body.linearVelocity=Vector3.back*12;
                        }
                        guard.SetHeld(false,Vector3.forward); guard.SetHeld(true,Vector3.forward);
                        Physics.SyncTransforms();
                        Assert.That(guard.TickGuard(.02f),Is.False,"Counter-generated fragments retriggered the guard.");
                        foreach(var child in children) child.Body.detectCollisions=false;
                        if(index==2)
                        {
                            // A later ordinary impact fracture must retain ancestry
                            // immunity without inheriting the counter's velocity clamp.
                            var secondary=children.OrderByDescending(c=>c.BreakRadius).First();
                            var oldIds=children.Select(c=>c.StableEarthId).ToArray();
                            pool.PrepareFracture(secondary.GetComponent<Collider>(),2);
                            float secondaryMass=secondary.EarthMass;
                            Assert.That(pool.TryEmitBreak(secondary.Body.position,Vector3.forward,Vector3.back*20f,
                                secondary.BreakRadius,secondaryMass,secondary.StableEarthId,
                                new EarthRockBreakDecision(true,2,0,0),1,secondary.MatterIdentity),Is.True,pool.LastBreakRejection);
                            secondary.ResetPiece();
                            var descendants=pool.GetComponentsInChildren<EarthRockDebris>(false)
                                .Where(c=>!oldIds.Contains(c.StableEarthId)).ToArray();
                            Assert.That(descendants.Length,Is.EqualTo(2));
                            Assert.That(descendants.Sum(c=>c.EarthMass),Is.EqualTo(secondaryMass).Within(.001f));
                            foreach(var descendant in descendants)
                            {
                                Assert.That(descendant.CounterGuardEligible,Is.False,"Ordinary descendant fracture lost counter ancestry.");
                                Assert.That(Vector3.Dot(descendant.Body.linearVelocity,Vector3.forward),Is.LessThan(-10f),
                                    "Ordinary fracture incorrectly reused the defensive momentum arrest.");
                                descendant.Body.position=body.position+Vector3.forward*1.5f;
                                descendant.Body.linearVelocity=Vector3.back*12f;
                                descendant.Body.detectCollisions=true;
                            }
                            Physics.SyncTransforms();
                            Assert.That(guard.TickGuard(.02f),Is.False,"Second-generation counter descendants retriggered guard.");
                            // A deliberate new magic ownership cycle makes this matter
                            // a fresh projectile, rather than permanently immune scenery.
                            descendants[0].OnEarthMagicGrabbed(EarthMagicGripKind.Telekinesis);
                            descendants[0].OnEarthMagicReleased(EarthMagicGripKind.Telekinesis);
                            Assert.That(descendants[0].CounterGuardEligible,Is.True,"Explicit magic release must start fresh eligibility.");
                            foreach(var descendant in descendants)descendant.Body.detectCollisions=false;
                        }
                    }
                    guard.SetHeld(false,Vector3.forward);
                    Assert.That(guard.IsGuarding,Is.False);
                    Object.Destroy(source); source=null;
                }
            }
            finally
            {
                if(source!=null) Object.Destroy(source);
                Object.Destroy(defender);Object.Destroy(poolObject);Object.Destroy(template);Object.Destroy(host);
            }
            yield return null;
        }
    }
}
