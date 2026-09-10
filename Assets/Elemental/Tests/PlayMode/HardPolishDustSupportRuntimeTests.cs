using System.Collections;
using Elemental.Presentation.VFX;
using Elemental.Simulation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Elemental.Tests.PlayMode
{
    public sealed class HardPolishDustSupportRuntimeTests
    {
        [UnityTest] public IEnumerator QueriesStayBoundedAndDisabledSupportRetiresWithoutRebirth()
        {
            var root=new GameObject("Dust support acceptance");
            var profile=ScriptableObject.CreateInstance<EarthSurfaceWindDustProfile>();
            var material=new Material(Shader.Find("Elemental/Light Dust Mote"));
            try
            {
                var planet=new GameObject("Planet");planet.transform.SetParent(root.transform);planet.transform.position=new Vector3(0,-100,0);
                var anchor=new GameObject("Anchor");anchor.transform.SetParent(root.transform);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(root.transform);
                floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);floor.layer=31;
                var emitter=new GameObject("Emitter");emitter.transform.SetParent(root.transform);emitter.SetActive(false);
                emitter.AddComponent<ParticleSystem>();var dust=emitter.AddComponent<EarthSurfaceWindDust>();
                profile.clusteredWisps=true;profile.maximumParticles=112;profile.areaRadius=2;profile.groundRate=40;profile.stoneRate=0;
                profile.lifetimeSeconds=new Vector2(1.2f,2.4f);profile.hoverHeight=.04f;profile.opacity=.18f;
                dust.Configure(profile,anchor.transform,planet.transform,1<<31,material);emitter.SetActive(true);
                Physics.SyncTransforms();
                double deadline=Time.realtimeSinceStartupAsDouble+2;
                int maximum=0;
                while(Time.realtimeSinceStartupAsDouble<deadline)
                {
                    yield return new WaitForEndOfFrame();
                    maximum=Mathf.Max(maximum,dust.LiveParticles);
                    Assert.That(dust.SupportQueriesLastFrame,Is.LessThanOrEqualTo(EarthDustSupportPolicy.MaximumRefreshQueries));
                    Assert.That(dust.SupportQueriesLastFrame+dust.BirthQueriesLastFrame,Is.LessThanOrEqualTo(EarthDustSupportPolicy.MaximumQueriesPerFrame));
                    Assert.That(dust.LiveParticles,Is.LessThanOrEqualTo(112));
                }
                Assert.That(maximum,Is.GreaterThan(10),"Fixture did not exercise live support scheduling.");
                int before=dust.InvalidatedSupportCount;
                floor.GetComponent<Collider>().enabled=false;
                yield return new WaitForEndOfFrame();
                Assert.That(dust.InvalidatedSupportCount,Is.GreaterThan(before),"Disabled cached support must invalidate before its next raycast turn.");
                yield return new WaitForSeconds(.4f);
                Assert.That(dust.LiveParticles,Is.Zero,"Lost support should retire through alpha without resetting particle age or rebirthing over empty space.");
            }
            finally { Object.Destroy(root);Object.Destroy(profile);Object.Destroy(material); }
        }
    }
}
