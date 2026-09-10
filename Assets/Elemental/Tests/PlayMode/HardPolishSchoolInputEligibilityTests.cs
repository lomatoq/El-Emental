using System.Collections;
using Elemental.Input.Actions;
using Elemental.Presentation.UI;
using Elemental.Simulation.Bending;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using MouseButton=UnityEngine.InputSystem.LowLevel.MouseButton;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishSchoolInputTests
    {
        [UnityTest,Timeout(240000)] public IEnumerator HudPausePressNeverStartsFireAndLostContextCannotAcquireEarth()
        {
            yield return KeyTap(Key.Digit1);Primary(false);yield return new WaitForEndOfFrame();
            var document=All<EarthDuelHud>()[0].GetComponent<UIDocument>().rootVisualElement;
            var pause=document.Q<Button>("pause-match");Assert.That(pause,Is.Not.Null);
            Vector2 panelPoint=pause.worldBound.center;Rect panel=document.panel.visualTree.worldBound;
            Assert.That(panel.width,Is.GreaterThan(0));Assert.That(panel.height,Is.GreaterThan(0));
            Vector2 pixel=new Vector2((panelPoint.x-panel.x)*Screen.width/panel.width,Screen.height-(panelPoint.y-panel.y)*Screen.height/panel.height);
            int begins=binding.PoseBegins;uint generation=binding.PlayerSession.Generation;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=pixel}.WithButton(MouseButton.Left));yield return null;
            Assert.That(magic.WorldPointerEligible,Is.False,"Fixture must hit the production Pause button.");
            Assert.That(binding.PoseBegins,Is.EqualTo(begins),"HUD feedback latency must not admit a Fire session.");
            Assert.That(binding.PlayerSession.Generation,Is.EqualTo(generation));Assert.That(binding.PlayerSession.IsActive,Is.False);
            Primary(false);yield return new WaitForSecondsRealtime(.2f);if(flow.State==FrontendState.Paused)flow.Resume();yield return null;
            yield return KeyTap(Key.Digit2);
            var router=magic.GetComponent<EarthActionRouterBehaviour>();Assert.That(router,Is.Not.Null);
            try
            {
                magic.SendMessage("OnApplicationFocus",false);Primary(true);yield return new WaitForSecondsRealtime(.25f);
                Assert.That(router.Owner,Is.EqualTo(EarthActionOwner.None),"Unfocused Earth cannot acquire a mouse gesture.");
                Primary(false);yield return null;magic.SendMessage("OnApplicationFocus",true);yield return null;
                magic.SendMessage("OnApplicationPause",true);Primary(true);yield return new WaitForSecondsRealtime(.25f);
                Assert.That(router.Owner,Is.EqualTo(EarthActionOwner.None),"Application pause is a durable input state even if timeScale remains one.");
            }
            finally{Primary(false);magic.SendMessage("OnApplicationFocus",true);magic.SendMessage("OnApplicationPause",false);}
        }
    }
}
