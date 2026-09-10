using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Elemental.Presentation.UI;
using Elemental.Presentation.Rendering;
using Elemental.Simulation.Magic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
namespace Elemental.Tests.PlayMode
{
    public sealed partial class HardPolishSchoolInputTests
    {
        [UnityTest,Timeout(300000)] public IEnumerator SchoolInstructionsStayAbsentAcrossSelectionAndHudRebuildAtNative1080()
        {
            const string folder="BuildReports/HardPolish/G05/SchoolHint";Directory.CreateDirectory(folder);
            using(var resolution=new ProductionCaptureResolution())
            {
                yield return resolution.WaitForRenderedSize(All<CelestialSystemBehaviour>().Single().TargetCamera);
                var hud=All<EarthDuelHud>().Single();
                for(int phase=0;phase<4;phase++)
                {
                    if(phase==1)Assert.That(magic.TrySelectElement(ElementId.Fire),Is.True);
                    if(phase==2){flow.Pause();yield return null;flow.Resume();}
                    if(phase==3){hud.enabled=false;yield return null;hud.enabled=true;}
                    yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();
                    var hint=hud.GetComponent<UIDocument>().rootVisualElement.Q<Label>("element-action-hint");
                    Assert.That(hint,Is.Null,"Instruction text must stay absent for every school and after HUD reconstruction.");
                    ProductionCaptureResolution.SaveScreen(folder+"/readable-"+phase+".png");
                }
            }
        }
    }
}
