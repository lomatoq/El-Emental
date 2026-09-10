using System.Collections;
using System.Linq;
using System.Reflection;
using Elemental.Runtime.Fire;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [UnityTest,Timeout(240000)]public IEnumerator SustainedBurnCoversActualMeshAreaAndFollowsReceiver()
        {
            GameObject target=null;FireVisualCaptureCamera capture=null;float step=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
                target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Mesh area sustained burn receiver";
                target.transform.position=flightMotor.Body.position+flightMotor.LocalUp*6;target.transform.localScale=new Vector3(3,2,.6f);
                var renderer=target.GetComponent<MeshRenderer>();
#if UNITY_EDITOR
                renderer.sharedMaterial=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat");
#endif
                var camera=All<Elemental.Presentation.Rendering.CelestialSystemBehaviour>().Single().TargetCamera;
                capture=camera.gameObject.AddComponent<FireVisualCaptureCamera>();capture.Place(target.transform.position+new Vector3(2,1.2f,4),target.transform.position);
                var collider=target.GetComponent<Collider>();var response=binding.PlayerSession.GetComponent<FireWorldImpact>();
                for(int i=0;i<48;i++){Physics.SyncTransforms();response.ApplyContact(collider,target.transform.position+Vector3.forward*.3f,Vector3.forward,Vector3.back,.05f,1);yield return new WaitForSeconds(.05f);}
                var burning=binding.GetComponent<FireSmolderPresentation>();
                Assert.That(burning.SurfaceSamples,Is.GreaterThanOrEqualTo(12),"A heated area must have distinct surface roots, not stacked cards at one point.");
                Assert.That(burning.UnsupportedBurnMeshes,Is.Zero);
                var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                var seats=(System.Array)typeof(FireSmolderPresentation).GetField("seats",flags).GetValue(burning);
                object seat=null;foreach(var candidate in seats)if(candidate.GetType().GetField("Root").GetValue(candidate) as Transform==target.transform){seat=candidate;break;}
                Assert.That(seat,Is.Not.Null);var type=seat.GetType();var roots=(Vector3[])type.GetField("BurnPoints").GetValue(seat);int count=(int)type.GetField("BurnPointsCount").GetValue(seat);
                float span=0;for(int i=0;i<count;i++){Assert.That(roots[i].z,Is.EqualTo(.5f).Within(.002f),"Anchors must stay on the struck face, never the rear face or floating plane.");for(int j=0;j<i;j++)span=Mathf.Max(span,Vector3.Distance(target.transform.TransformPoint(roots[i]),target.transform.TransformPoint(roots[j])));}
                Assert.That(span,Is.GreaterThan(1));
                var lights=binding.GetComponent<FireAbilityLighting>().GetComponentsInChildren<Light>().Where(x=>x.name.StartsWith("Fire ability radiance ")&&int.Parse(x.name.Substring("Fire ability radiance ".Length))>=8).ToArray();
                Assert.That(lights.Any(x=>x.enabled),Is.True);Assert.That(lights.Where(x=>x.enabled).Max(x=>x.intensity),Is.LessThan(.5f));
                yield return new WaitForEndOfFrame();SaveFireAbilityFrame("mesh-area-burning-hovl");Debug.Log("Surface burn: anchors="+burning.SurfaceSamples+", cards="+burning.IgnitionParticles+", CPU atlas update ms="+burning.IgnitionStepMilliseconds.ToString("F4",System.Globalization.CultureInfo.InvariantCulture));
                Vector3 before=target.transform.TransformPoint(roots[0]);target.transform.position+=Vector3.right*.4f;yield return null;
                Assert.That(Vector3.Distance(target.transform.TransformPoint(roots[0]),before+Vector3.right*.4f),Is.LessThan(.001f));
                yield return new WaitForSeconds(1.3f);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("mesh-area-burning-cooling");
                yield return new WaitForSeconds(7);Assert.That(burning.ActiveIgnitions,Is.Zero);Assert.That(burning.SurfaceSamples,Is.Zero);
            }
            finally{if(capture!=null)Object.Destroy(capture);if(target!=null)Object.Destroy(target);Time.captureDeltaTime=step;ReleaseFireAbilityFixture();}
        }
    }
}
