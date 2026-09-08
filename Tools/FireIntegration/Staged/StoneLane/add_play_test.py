from pathlib import Path
lane=Path('El-Emental/Tools/FireIntegration/Staged/StoneLane'); dest=lane/'after/Assets/Elemental/Tests/PlayMode/EarthStoneImpactDustRuntimeTests.cs'; dest.parent.mkdir(parents=True,exist_ok=True)
dest.write_text('''using System.Collections;
using Elemental.Presentation.VFX;
using Elemental.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Elemental.Tests.PlayMode
{
    public sealed class EarthStoneImpactDustRuntimeTests
    {
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
                var renderer=chips.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer.meshCount,Is.EqualTo(4));
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,2,.6f,1,1,1); hub.FlushPending();
                int lightDust=dust.particleCount;
                dust.Clear(); chips.Clear();
                hub.EmitStoneImpact(Vector3.zero,Vector3.up,1000,.6f,1,2,1); hub.FlushPending();
                Assert.That(dust.particleCount,Is.GreaterThan(lightDust),"Production tuning must preserve mass response.");
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
''',encoding='utf-8')
print(dest)
