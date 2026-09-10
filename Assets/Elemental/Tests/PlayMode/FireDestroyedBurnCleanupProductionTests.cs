using System.Collections;
using System.Reflection;
using Elemental.Runtime.Fire;
using Elemental.Presentation.Fire;
using Elemental.Simulation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishFireStreamBindingRuntimeTests
    {
        [UnityTest,Timeout(240000)]public IEnumerator SustainedSkinAnchorsFollowBoneWithoutNewHeatContact()
        {
            GameObject target=null;Mesh mesh=null;float savedStep=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
                target=new GameObject("Single-bone heated surface regression");
                target.transform.position=flightMotor.Body.position+flightMotor.LocalUp*6;
                var bone=new GameObject("Heated moving bone").transform;bone.SetParent(target.transform,false);
                var skin=target.AddComponent<SkinnedMeshRenderer>();skin.updateWhenOffscreen=true;
                mesh=new Mesh{name="Readable two-triangle animated burn patch"};
                mesh.vertices=new[]{new Vector3(-1.5f,-1,.3f),new Vector3(1.5f,-1,.3f),new Vector3(1.5f,1,.3f),new Vector3(-1.5f,1,.3f)};
                mesh.triangles=new[]{0,1,2,0,2,3};
                var weight=new BoneWeight{boneIndex0=0,weight0=1};mesh.boneWeights=new[]{weight,weight,weight,weight};
                mesh.bindposes=new[]{bone.worldToLocalMatrix*target.transform.localToWorldMatrix};mesh.RecalculateNormals();mesh.RecalculateBounds();
                skin.sharedMesh=mesh;skin.bones=new[]{bone};skin.rootBone=bone;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*5);
#if UNITY_EDITOR
                skin.sharedMaterial=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Elemental/Content/GraphicsV5/Materials/RumbleSandstone.mat");
#endif
                var collider=target.AddComponent<BoxCollider>();collider.size=new Vector3(3,2,.6f);
                var response=binding.PlayerSession.GetComponent<FireWorldImpact>();
                for(int i=0;i<18;i++)
                {
                    Physics.SyncTransforms();
                    Assert.That(response.ApplyContact(collider,target.transform.position+Vector3.forward*.3f,Vector3.forward,Vector3.back,.05f,1),Is.True);
                    yield return new WaitForSeconds(.05f);
                }
                var burning=binding.GetComponent<FireSmolderPresentation>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var seats=(System.Array)typeof(FireSmolderPresentation).GetField("seats",flags).GetValue(burning);
                object owned=null;foreach(var seat in seats)if(seat.GetType().GetField("Root").GetValue(seat) as Transform==target.transform){owned=seat;break;}
                Assert.That(owned,Is.Not.Null);var type=owned.GetType();
                int count=(int)type.GetField("BurnPointsCount").GetValue(owned);Assert.That(count,Is.GreaterThanOrEqualTo(8));
                var roots=(Vector3[])type.GetField("BurnPoints").GetValue(owned);var before=new Vector3[count];System.Array.Copy(roots,before,count);
                bone.localPosition=Vector3.right*.6f;
                // No ApplyContact call follows this pose change. Residual burning must
                // stay attached to the original mesh triangles instead of a new world patch.
                yield return new WaitForSeconds(.35f);yield return new WaitForEndOfFrame();
                Assert.That((int)type.GetField("Flame").GetValue(owned),Is.GreaterThanOrEqualTo(0));
                Assert.That((int)type.GetField("BurnPointsCount").GetValue(owned),Is.EqualTo(count));
                for(int i=0;i<count;i++)Assert.That(Vector3.Distance(roots[i],before[i]+Vector3.right*.6f),Is.LessThan(.02f));
            }
            finally
            {
                if(target!=null)Object.Destroy(target);if(mesh!=null)Object.Destroy(mesh);
                Time.captureDeltaTime=savedStep;ReleaseFireAbilityFixture();
            }
        }
        [UnityTest,Timeout(240000)]public IEnumerator DestroyedBurnReceiverReleasesVisibleFlameAndReusesCleanSeat()
        {
            GameObject target=null,replacement=null;float savedStep=Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();
                target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Destroyed burning receiver regression";
                target.transform.position=flightMotor.Body.position+flightMotor.LocalUp*6;
                target.transform.localScale=new Vector3(3,2,.6f);
                var response=binding.PlayerSession.GetComponent<FireWorldImpact>();
                var collider=target.GetComponent<Collider>();
                for(int i=0;i<18;i++)
                {
                    Physics.SyncTransforms();
                    Assert.That(response.ApplyContact(collider,target.transform.position+Vector3.forward*.3f,Vector3.forward,Vector3.back,.05f,1),Is.True);
                    yield return new WaitForSeconds(.05f);
                }
                var burning=binding.GetComponent<FireSmolderPresentation>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var seats=(System.Array)typeof(FireSmolderPresentation).GetField("seats",flags).GetValue(burning);
                object owned=null;
                foreach(var candidate in seats)if(candidate.GetType().GetField("Root").GetValue(candidate) as Transform==target.transform){owned=candidate;break;}
                Assert.That(owned,Is.Not.Null);var type=owned.GetType();
                int flame=(int)type.GetField("Flame").GetValue(owned);Assert.That(flame,Is.GreaterThanOrEqualTo(0));
                var renderers=(System.Array)typeof(FireSmolderPresentation).GetField("flames",flags).GetValue(burning);
                object renderer=renderers.GetValue(flame);
                Assert.That((bool)renderer.GetType().GetProperty("Visible").GetValue(renderer),Is.True);
                Vector3 position=target.transform.position;Object.Destroy(target);target=null;
                yield return null;yield return new WaitForEndOfFrame();
                Assert.That((bool)renderer.GetType().GetProperty("Visible").GetValue(renderer),Is.False,"Destroyed receiver must not leave its last flame mesh enabled.");
                Assert.That((int)type.GetField("Flame").GetValue(owned),Is.EqualTo(-1));
                var owners=(System.Array)typeof(FireSmolderPresentation).GetField("flameOwners",flags).GetValue(burning);
                Assert.That(owners.GetValue(flame),Is.Null);
                Assert.That(((FireThermalState)type.GetField("State").GetValue(owned)).Heat,Is.Zero);
                Assert.That((int)type.GetField("BurnPointsCount").GetValue(owned),Is.Zero);
                replacement=GameObject.CreatePrimitive(PrimitiveType.Cube);replacement.name="Clean replacement burn receiver";replacement.transform.position=position;
                Physics.SyncTransforms();
                Assert.That(response.ApplyContact(replacement.GetComponent<Collider>(),position+Vector3.forward*.5f,Vector3.forward,Vector3.back,.01f,.1f),Is.True);
                object fresh=null;
                foreach(var candidate in seats)if(candidate.GetType().GetField("Root").GetValue(candidate) as Transform==replacement.transform){fresh=candidate;break;}
                Assert.That(fresh,Is.SameAs(owned),"Freed first seat should be reused, not leaked.");
                Assert.That((int)type.GetField("Flame").GetValue(fresh),Is.EqualTo(-1));
                Assert.That((int)type.GetField("BurnPointsCount").GetValue(fresh),Is.Zero);
                Assert.That(((FireThermalState)type.GetField("State").GetValue(fresh)).Heat,Is.EqualTo(.016f).Within(.0001f));
            }
            finally
            {
                if(target!=null)Object.Destroy(target);if(replacement!=null)Object.Destroy(replacement);
                Time.captureDeltaTime=savedStep;ReleaseFireAbilityFixture();
            }
        }
    }
}
