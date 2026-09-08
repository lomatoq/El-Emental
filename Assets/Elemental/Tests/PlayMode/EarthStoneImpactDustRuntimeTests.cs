using System.Collections;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthStoneImpactDustRuntimeTests
    {
        [UnityTest]
        public IEnumerator ActualFloorMakesCosmeticChipBounceThenStopAtSixtyHz() => ChipFloorContact(60);

        [UnityTest]
        public IEnumerator SlowFrameSweepDoesNotTunnelThroughThinFloor() => ChipFloorContact(10);

        private IEnumerator ChipFloorContact(int captureRate)
        {
#if UNITY_EDITOR
            var source = AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>("Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset");
            Assert.That(source, Is.Not.Null);
            var profile = Object.Instantiate(source);
            var go = new GameObject("Physical cosmetic chip contact proof");
            go.SetActive(false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var planet = new GameObject("Contact test planet centre");
            int oldRate = Time.captureFramerate;
            try
            {
                Vector3 origin = new Vector3(14000f, 0f, 0f);
                floor.transform.position = origin - Vector3.up * .025f;
                floor.transform.localScale = new Vector3(10f, .05f, 10f);
                planet.transform.position = origin - Vector3.up * 100f;
                var hub = go.AddComponent<EarthMaterialFeedbackHub>();
                hub.Configure(profile, planet.transform);
                var dustGo = new GameObject("Dust"); dustGo.transform.SetParent(go.transform);
                var chipGo = new GameObject("Chips"); chipGo.transform.SetParent(go.transform);
                var dust = dustGo.AddComponent<ParticleSystem>();
                var chips = chipGo.AddComponent<ParticleSystem>();
                var presenter = go.AddComponent<EarthMaterialFeedbackPresenter>();
                presenter.Configure(hub, profile, planet.transform, dust, chips, null);
                // This fixture needs no camera. Keep native particles advancing offscreen.
                var chipMain = chips.main; chipMain.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var dustMain = dust.main; dustMain.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                go.SetActive(true);
                Time.captureFramerate = captureRate;
                UnityEngine.Physics.SyncTransforms();
                var frameEnd = new WaitForEndOfFrame();
                yield return frameEnd;
                // A controlled particle isolates collision response; all presentation
                // integration, gravity, sweep, bounce and secondary dust are production code.
                chips.Emit(new ParticleSystem.EmitParams {
                    position = origin + Vector3.up * .15f,
                    velocity = new Vector3(.5f, -8f, 0f), startSize = .1f,
                    startLifetime = 6f, startColor = Color.white, randomSeed = 40u
                }, 1);
                var particles = new ParticleSystem.Particle[chips.main.maxParticles];
                bool bounced = false, stopped = false;
                int stillFrames = 0, puffs = 0, measurements = 0;
                Vector3 stopPosition = Vector3.zero;
                long totalNs = 0, maximumNs = 0;
                using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Elemental.Earth.MaterialParticles", 128);
                Assert.That(recorder.Valid, Is.True);
                for (int frame = 0; frame < 90 && stillFrames < 4; frame++)
                {
                    yield return frameEnd;
                    int count = chips.GetParticles(particles);
                    Assert.That(count, Is.EqualTo(1), "Controlled chip must stay alive through collision verification.");
                    ParticleSystem.Particle particle = particles[0];
                    int contacts = (int)(particle.randomSeed & 3u);
                    Assert.That(particle.position.y, Is.GreaterThanOrEqualTo(-.005f),
                        $"Native particle crossed the5cm floor at{captureRate}Hz, frame{frame}, contacts={contacts}.");
                    if (contacts == 1)
                    {
                        bounced = true;
                        Assert.That(particle.velocity.y, Is.GreaterThan(-3f), "First contact must dissipate the incoming downward speed.");
                    }
                    if (contacts == 3)
                    {
                        Assert.That(particle.velocity.sqrMagnitude, Is.LessThan(.000001f));
                        if (!stopped) { stopped = true; stopPosition = particle.position; }
                        else Assert.That(Vector3.Distance(stopPosition, particle.position), Is.LessThan(.002f), "Stopped chip must not drift or fall through the floor.");
                        stillFrames++;
                    }
                    puffs = Mathf.Max(puffs, dust.particleCount);
                    if (recorder.LastValue > 0) { totalNs += recorder.LastValue; maximumNs = System.Math.Max(maximumNs, recorder.LastValue); measurements++; }
                }
                Assert.That(bounced, Is.True, "Actual floor must produce the first bounce.");
                Assert.That(stopped && stillFrames >= 4, Is.True, "The chip must settle and stay stopped for four rendered steps.");
                Assert.That(puffs, Is.GreaterThan(0), "Real collision must emit secondary contact dust.");
                Assert.That(measurements, Is.GreaterThan(0));
                System.IO.Directory.CreateDirectory("BuildReports/StoneChipContact");
                System.IO.File.WriteAllText($"BuildReports/StoneChipContact/Contact-{captureRate}Hz.txt",
                    $"captureRate={captureRate}; samples={measurements}; meanMs={totalNs / (double)measurements / 1000000d:F6}; peakMs={maximumNs / 1000000d:F6}; bounce={bounced}; stoppedFrames={stillFrames}; secondaryPuffs={puffs}");
            }
            finally
            {
                Time.captureFramerate = oldRate;
                Object.Destroy(go); Object.Destroy(floor); Object.Destroy(planet); Object.Destroy(profile);
            }
#else
            Assert.Ignore("Production profile contact proof requires Editor PlayMode.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ProductionPresenterUsesFourMeshesAndPreservesHeavyDropResponse()
        {
#if UNITY_EDITOR
            var source=AssetDatabase.LoadAssetAtPath<EarthEffectsTuningProfile>("Assets/Elemental/Content/Profiles/EarthEffectsTuningProfile.asset");
            Assert.That(source,Is.Not.Null);
            var profile=Object.Instantiate(source);
            var go=new GameObject("Stone impact dust runtime proof"); go.SetActive(false);
            try
            {
                var hub=go.AddComponent<EarthMaterialFeedbackHub>(); hub.Configure(profile,null);
                var dustGo=new GameObject("Dust"); dustGo.transform.SetParent(go.transform);
                var chipGo=new GameObject("Chips"); chipGo.transform.SetParent(go.transform);
                var dust=dustGo.AddComponent<ParticleSystem>(); var chips=chipGo.AddComponent<ParticleSystem>();
                var presenter=go.AddComponent<EarthMaterialFeedbackPresenter>();
                presenter.Configure(hub,profile,null,dust,chips,null);
                go.SetActive(true);
                var softDust=go.transform.Find("Original Soft Contact Dust").GetComponent<ParticleSystem>();
                var renderer=chips.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer.meshCount,Is.EqualTo(4));
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,2,.6f,1,1,1); hub.FlushPending();
                int lightDust=dust.particleCount+softDust.particleCount;
                dust.Clear(); softDust.Clear(); chips.Clear();
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,2,1); hub.FlushPending();
                Assert.That(dust.particleCount+softDust.particleCount,Is.GreaterThan(lightDust),"Production tuning must preserve mass response.");
                var layerParticles = new ParticleSystem.Particle[Mathf.Max(dust.main.maxParticles,softDust.main.maxParticles)];
                int layerMask = 0;
                float longestContact = 0f, shortestResidual = float.MaxValue;
                foreach (var layer in new[] { dust, softDust })
                {
                  int layerCount = layer.GetParticles(layerParticles);
                  for (int i=0;i<layerCount;i++)
                  {
                    int role=(int)(layerParticles[i].randomSeed&3u); layerMask|=1<<role;
                    if(role==0) longestContact=Mathf.Max(longestContact,layerParticles[i].startLifetime);
                    if(role==2) shortestResidual=Mathf.Min(shortestResidual,layerParticles[i].startLifetime);
                  }
                }
                Assert.That(layerMask,Is.EqualTo(7),"Heavy impacts need contact, main cloud and residual roles.");
                Assert.That(shortestResidual,Is.GreaterThan(longestContact),"Residual haze must outlive contact grit.");
                // Several separated identities sample actual native mesh selection, without bypassing the hub.
                for(uint i=3;i<12;i++)
                { hub.EmitStoneImpact(Vector3.right*i,Vector3.up,1000,1.5f,1,i,1); hub.FlushPending(); }
                var particles=new ParticleSystem.Particle[chips.main.maxParticles];
                int count=chips.GetParticles(particles), mask=0;
                Assert.That(count,Is.GreaterThan(16));
                for(int i=0;i<count;i++)
                {
                    int index=particles[i].GetMeshIndex(chips);
                    Assert.That(index,Is.InRange(0,3)); mask|=1<<index;
                }
                Assert.That(mask,Is.EqualTo(15),"All four silhouettes must actually be selected by emitted particles.");
                int eventsAfterEnable=0; hub.Presented+=_=>eventsAfterEnable++;
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,500,1); hub.FlushPending();
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,500,1); hub.FlushPending();
                Assert.That(eventsAfterEnable,Is.EqualTo(1));
                go.SetActive(false); go.SetActive(true);
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,500,1); hub.FlushPending();
                Assert.That(eventsAfterEnable,Is.EqualTo(2),"Actual Play lifecycle must reset impact cooldown.");
                yield return null;
            }
            finally { Object.Destroy(go); Object.Destroy(profile); }
#else
            Assert.Ignore("Production asset proof requires Editor PlayMode.");
            yield break;
#endif
        }
    }
}
