using System.Collections;
using System.Reflection;
using Elemental.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Elemental.Tests.PlayMode
{
    public sealed class HudMotionPlayTests
    {
        [UnityTest] public IEnumerator PausePresentsPressBeforeDispatchAndDoesNotMoveAuthoredButton()
        {
            var go=new GameObject("HUD pause motion contract");go.SetActive(false);
            float originalTimeScale=Time.timeScale;
            try
            {
                var hud=go.AddComponent<EarthDuelHud>();
                var button=new Button();button.style.translate=new Translate(27,31);button.style.scale=new Scale(new Vector3(1.2f,1.2f,1));
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(EarthDuelHud).GetField("_pause",flags).SetValue(hud,button);
                int dispatched=0;hud.ConfigurePause(()=>dispatched++);
                var request=typeof(EarthDuelHud).GetMethod("RequestPauseWithFeedback",flags);
                var tick=typeof(EarthDuelHud).GetMethod("TickPauseMotion",flags);
                request.Invoke(hud,null);tick.Invoke(hud,null);
                Assert.That(dispatched,Is.Zero,"The pressed visual must survive at least one rendered frame.");
                Assert.That(button.style.scale.value.value.x,Is.EqualTo(1.08f).Within(.0001f));
                Time.timeScale=0;
                double deadline=Time.realtimeSinceStartupAsDouble+1;
                while(dispatched==0&&Time.realtimeSinceStartupAsDouble<deadline){yield return null;tick.Invoke(hud,null);}
                Assert.That(dispatched,Is.EqualTo(1));
                tick.Invoke(hud,null);Assert.That(dispatched,Is.EqualTo(1));
                Assert.That(button.style.scale.value.value.x,Is.EqualTo(1.2f).Within(.0001f));
                Assert.That(button.style.translate.value.x.value,Is.EqualTo(27));
                Assert.That(button.style.translate.value.y.value,Is.EqualTo(31));
            }
            finally{Time.timeScale=originalTimeScale;Object.DestroyImmediate(go);}
        }
    }
}
