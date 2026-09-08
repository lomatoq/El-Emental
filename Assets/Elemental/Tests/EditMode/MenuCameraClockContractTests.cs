using Elemental.Presentation.UI;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class MenuCameraClockContractTests
    {
        [TestCase(1.777778f,10f),TestCase(1.333333f,10f),TestCase(2.333333f,0f)]
        public void PortraitUsesRequestedViewportPositionAcrossAspectAndRoll(float aspect,float roll)
        {
            var host=new GameObject("Viewport framing contract",typeof(Camera));
            try
            {
                var camera=host.GetComponent<Camera>();camera.fieldOfView=38;camera.aspect=aspect;
                Vector3 subject=new Vector3(1,2,0),position=new Vector3(1,3,6);
                Vector2 requested=new Vector2(.78f,.58f);
                camera.transform.SetPositionAndRotation(position,CinematicMenuCamera.PortraitRotation(position,subject,Vector3.up,38,aspect,requested,roll)*Quaternion.AngleAxis(roll,Vector3.forward));
                Vector3 rendered=camera.WorldToViewportPoint(subject);
                Assert.That(rendered.z,Is.GreaterThan(0));
                Assert.That(rendered.x,Is.EqualTo(requested.x).Within(.0001f));Assert.That(rendered.y,Is.EqualTo(requested.y).Within(.0001f));
            }
            finally{Object.DestroyImmediate(host);}
        }
        [TestCase(false), TestCase(true)] public void PresentationScopesBrainClockAndTracksActorWarp(bool previousIgnoreTimeScale)
        {
            var root=new GameObject("Menu camera contract");
            try
            {
                var outputObject=new GameObject("Output",typeof(Camera),typeof(CinemachineBrain));outputObject.transform.SetParent(root.transform);
                var menuObject=new GameObject("Menu",typeof(CinemachineCamera));menuObject.transform.SetParent(root.transform);
                var actor=new GameObject("Actor");actor.transform.SetParent(root.transform);
                var brain=outputObject.GetComponent<CinemachineBrain>();
                brain.IgnoreTimeScale=previousIgnoreTimeScale;brain.UpdateMethod=CinemachineBrain.UpdateMethods.SmartUpdate;
                brain.BlendUpdateMethod=CinemachineBrain.BrainUpdateMethods.FixedUpdate;
                var presenter=root.AddComponent<CinematicMenuCamera>();
                presenter.Configure(menuObject.GetComponent<CinemachineCamera>(),brain,outputObject.GetComponent<Camera>(),null,null,null,null,actor.transform);
                presenter.Enter(false,.2f);
                Assert.That(brain.IgnoreTimeScale,Is.True);
                Assert.That(brain.UpdateMethod,Is.EqualTo(CinemachineBrain.UpdateMethods.LateUpdate));
                Assert.That(brain.BlendUpdateMethod,Is.EqualTo(CinemachineBrain.BrainUpdateMethods.LateUpdate));
                Assert.That(presenter.NeedsReframe,Is.False);
                actor.transform.position+=Vector3.right*3;Assert.That(presenter.NeedsReframe,Is.True);
                presenter.Reframe(false);Assert.That(presenter.NeedsReframe,Is.False);
                presenter.ReturnToGameplay(false,.2f);presenter.Enter(false,.2f);
                Assert.That(presenter.OwnsPresentation,Is.True,"Reopening cancels a pending return.");
                presenter.FinishCombatTransition();
                Assert.That(brain.IgnoreTimeScale,Is.EqualTo(previousIgnoreTimeScale));
                Assert.That(brain.UpdateMethod,Is.EqualTo(CinemachineBrain.UpdateMethods.SmartUpdate));
                Assert.That(brain.BlendUpdateMethod,Is.EqualTo(CinemachineBrain.BrainUpdateMethods.FixedUpdate));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
