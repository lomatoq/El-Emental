using System.Collections;
using System.Reflection;
using Elemental.Presentation.Rendering;
using Elemental.Presentation.UI;
using Elemental.Simulation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
    public sealed class HardPolishStormRuntimeTests
    {
        [UnityTest] public IEnumerator DisabledPausedAndAccessibilityModesClearPulseAndRumble()
        {
            var root=new GameObject("Storm acceptance");root.SetActive(false);
            var settings=root.AddComponent<FrontendFlowController>();
            var controller=root.AddComponent<ValleyAtmosphereController>();
            var profile=ScriptableObject.CreateInstance<ValleyAtmosphereProfile>();profile.CloudArt=Texture2D.whiteTexture;
            var audioRoot=new GameObject("Storm audio acceptance");var audio=audioRoot.AddComponent<AudioSource>();
            float previousScale=Time.timeScale;Time.timeScale=1;
            var clip=AudioClip.Create("Storm test silence",24000,1,24000,false);
            audio.clip=clip;
            Vector4 previous=Shader.GetGlobalVector("_ElementalValleyStorm");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(ValleyAtmosphereController);
            var publish=type.GetMethod("PublishStorm",flags);
            try
            {
                type.GetField("profile",flags).SetValue(controller,profile);
                type.GetField("stormAudio",flags).SetValue(controller,audio);
                controller.ConfigureStorm(settings,clip);
                yield return null;
                for(int mode=0;mode<4;mode++)
                {
                    settings.Preferences.Set(1,1,1,false);settings.Preferences.SetReducedFlashes(false);controller.StormEnabled=true;
                    typeof(FrontendFlowController).GetField("<State>k__BackingField",flags).SetValue(settings,FrontendState.Main);
                    var clock=new DistantStormClock();clock.Step(.001f,true);clock.Step(clock.NextInterval,true);clock.Step(.10f,true);
                    type.GetField("stormClock",flags).SetValue(controller,clock);publish.Invoke(controller,null);
                    Assert.That(controller.StormPower,Is.GreaterThan(0),"Fixture must first exercise an active pulse.");
                    audio.Play();yield return null;
                    Assert.That(audio.isPlaying,Is.True,"Fixture must exercise an audible-source lifecycle before suppression.");
                    if(mode==0)controller.StormEnabled=false;
                    if(mode==1)settings.Preferences.SetReducedFlashes(true);
                    if(mode==2)settings.Preferences.Set(1,1,1,true);
                    if(mode==3)typeof(FrontendFlowController).GetField("<State>k__BackingField",flags).SetValue(settings,FrontendState.Paused);
                    publish.Invoke(controller,null);
                    Assert.That(controller.StormPower,Is.Zero);Assert.That(Shader.GetGlobalVector("_ElementalValleyStorm").w,Is.Zero);
                    Assert.That(controller.StormRumblePlaying,Is.False);
                }
            }
            finally{Time.timeScale=previousScale;Object.DestroyImmediate(audioRoot);Object.DestroyImmediate(root);Object.DestroyImmediate(profile);Object.DestroyImmediate(clip);Shader.SetGlobalVector("_ElementalValleyStorm",previous);}
        }
    }
}
