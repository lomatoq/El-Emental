using System.Reflection;
using Elemental.Presentation.Animation;
using Elemental.Presentation.VFX;
using Elemental.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class BotAuthoredTintOwnershipTests
    {
        [Test] public void ProductionTelegraphPhasesPreserveAuthoredTeamAndRespawnEmission()
        {
            var actor=new GameObject("Authored team tint owner fixture");actor.SetActive(false);
            var meshObject=new GameObject("Authored team renderer");meshObject.transform.SetParent(actor.transform);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var renderer=meshObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                var shared=actor.AddComponent<HumanoidCharacterPresentation>();var presenter=actor.AddComponent<EarthMvpBotPresenter>();
                var tint=new Color(.061f,.332f,.973f,.83f);var legacyTint=new Color(.1f,.3f,.9f,.72f);
                var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",tint);block.SetColor("_Color",legacyTint);block.SetFloat("_RespawnEmission",.63f);renderer.SetPropertyBlock(block);
                presenter.Configure(null,null,new Renderer[]{renderer},null,null,null,shared);
                var apply=typeof(EarthMvpBotPresenter).GetMethod("ApplyPhase",BindingFlags.NonPublic|BindingFlags.Instance);
                Color firstEdge=Color.clear;bool edgeChanged=false;
                foreach(EarthMvpBotPhase phase in System.Enum.GetValues(typeof(EarthMvpBotPhase)))
                {
                    apply.Invoke(presenter,new object[]{phase});renderer.GetPropertyBlock(block);
                    Assert.That(block.GetColor("_BaseColor"),Is.EqualTo(tint),phase.ToString());
                    Assert.That(block.GetColor("_Color"),Is.EqualTo(legacyTint));Assert.That(block.GetFloat("_RespawnEmission"),Is.EqualTo(.63f));
                    Color edge=block.GetColor("_EdgeColor");if(firstEdge==Color.clear)firstEdge=edge;else edgeChanged|=edge!=firstEdge;
                }
                Assert.That(edgeChanged,Is.True,"The fix must preserve real phase edge feedback.");
                presenter.Configure(null,null,new Renderer[]{renderer});renderer.GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor"),Is.Not.EqualTo(tint),"Legacy stone-only enemy tint fallback remains explicit.");
            }
            finally{Object.DestroyImmediate(actor);Object.DestroyImmediate(material);}
        }
    }
}
