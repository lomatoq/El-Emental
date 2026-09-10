using Elemental.Presentation.UI;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
namespace Elemental.Tests.EditMode
{
    public sealed class MenuCameraClockContractTests
    {
        [TestCase(false,false,false,false,false,false)]
        [TestCase(false,false,true,false,false,true)]
        [TestCase(false,false,false,true,false,true)]
        [TestCase(false,false,false,false,true,true)]
        [TestCase(true,false,true,true,true,false)]
        [TestCase(false,true,true,true,true,false)]
        public void LiveMenuDoesNotOwnPauseButMatchBoundariesDo(bool network,bool paused,bool transition,bool reset,bool finished,bool expected)
        {Assert.That(Elemental.Simulation.Time.FrontendWorldClockPolicy.ShouldHold(network,paused,transition,reset,finished),Is.EqualTo(expected));}
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
        [TestCase(false), TestCase(true)]
        public void DeparturePreservesRenderedStartAndUsesSavedBasisThroughInterruption(bool reduced)
        {
            var root = new GameObject("Departure contract");
            try
            {
                var output = new GameObject("Output", typeof(Camera), typeof(CinemachineBrain)); output.transform.SetParent(root.transform);
                var menuObject = new GameObject("Menu", typeof(CinemachineCamera)); menuObject.transform.SetParent(root.transform);
                var gameplayObject = new GameObject("Gameplay", typeof(CinemachineCamera)); gameplayObject.transform.SetParent(root.transform);
                gameplayObject.transform.position = new Vector3(0, 2, 40);
                var actor = new GameObject("Actor"); actor.transform.SetParent(root.transform);
                var camera = output.GetComponent<Camera>(); var menu = menuObject.GetComponent<CinemachineCamera>();
                menu.Priority = -23; var originalLens = menu.Lens;
                var presenter = root.AddComponent<CinematicMenuCamera>();
                presenter.Configure(menu, output.GetComponent<CinemachineBrain>(), camera, null, null, null, null, actor.transform,
                    gameplay: gameplayObject.GetComponent<CinemachineCamera>());
                presenter.Enter(reduced, .2f);
                // Simulate a final brain pose differing from the virtual camera during a blend.
                camera.transform.SetPositionAndRotation(new Vector3(2, 3, 7), Quaternion.Euler(11, 193, 7)); camera.fieldOfView = 37;
                Vector3 start = camera.transform.position; Quaternion rotation = camera.transform.rotation;
                presenter.CaptureDepartureStart(); actor.transform.position += Vector3.right;
                presenter.Reframe(reduced);
                Assert.That(Vector3.Distance(menu.transform.position, start), Is.LessThan(.0001f), "Readiness wait must retain the rendered click pose.");
                presenter.BeginCountdown(reduced, .2f);
                Assert.That(Vector3.Distance(menu.transform.position, start), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(menu.transform.rotation, rotation), Is.LessThan(.01f));
                Assert.That(menu.Lens.FieldOfView, Is.EqualTo(37).Within(.001f));
                Assert.That(menu.Lens.Dutch, Is.Zero, "The output rotation already includes Dutch.");
                presenter.SetDepartureProgress(.3f, reduced); Vector3 partial = menu.transform.position; Quaternion partialRotation = menu.transform.rotation;
                presenter.SetDepartureProgress(.8f, reduced); presenter.SetDepartureProgress(.3f, reduced);
                Assert.That(Vector3.Distance(menu.transform.position, partial), Is.LessThan(.0001f), "Sampling is independent of previous samples.");
                Assert.That(Quaternion.Angle(menu.transform.rotation, partialRotation), Is.LessThan(.01f));
                presenter.SetDepartureProgress(1, reduced); Assert.That(presenter.DepartureComplete, Is.True);
                Vector3 endpoint = menu.transform.position;
                presenter.SetCountdownDollyProgress(0, 0, reduced);
                Assert.That(Vector3.Distance(menu.transform.position, endpoint), Is.LessThan(.0001f));
                presenter.FinishCombatTransition();
                Assert.That((int)menu.Priority, Is.EqualTo(-23)); Assert.That(menu.Lens.FieldOfView, Is.EqualTo(originalLens.FieldOfView));
            }
            finally { Object.DestroyImmediate(root); }
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
