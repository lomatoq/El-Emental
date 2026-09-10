using System.Collections;
using System.Linq;
using Elemental.Presentation.Fire;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(180000)]public IEnumerator ActualFireCoolingUsesSharedDustMaterialAndVisibleSmokeSubmesh()
  {
   System.IDisposable safe=null;float saved=Time.captureDeltaTime;
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();safe=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var dust=Resources.Load<Material>("FireSmokeDustMaterial");Assert.That(dust,Is.Not.Null);Assert.That(dust.shader.isSupported,Is.True);
    Assert.That(dust.GetFloat("_FlipbookColumns"),Is.EqualTo(3));Assert.That(dust.GetTexture("_BaseMap"),Is.Not.Null);
    flightAbility.SetLiftHeld(true);yield return new WaitForSeconds(.6f);yield return new WaitForEndOfFrame();
    var effects=binding.GetComponent<FireAbilityEffects>();var renderers=effects.GetComponentsInChildren<MeshRenderer>().Where(x=>x.name=="Transported flame and smoke atlas accents"&&x.enabled).ToArray();
    int smokeIndices=0;foreach(var renderer in renderers){Assert.That(renderer.sharedMaterials.Length,Is.EqualTo(2));Assert.That(renderer.sharedMaterials[1],Is.SameAs(dust));var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;smokeIndices+=(int)mesh.GetIndexCount(1);}
    Assert.That(smokeIndices,Is.GreaterThan(0),"Cooling gas must submit actual dust smoke geometry.");SaveFireAbilityFrame("shared-dust-cooling-feet");
    flightAbility.CancelAll();yield return new WaitForSeconds(.22f);yield return new WaitForEndOfFrame();SaveFireAbilityFrame("shared-dust-cooling-release");
   }
   finally{safe?.Dispose();Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();}
  }
 }
}
