// Tools-only Unity_RunCommand replay in actual Combat; no camera, UI or scene mutation.
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Elemental.Presentation.UI;
internal class CommandScript:IRunCommand
{
    [Serializable] private class Item{public string name,text,display,visibility,picking;public Rect panelBounds,normalizedBounds;public float fontSize;}
    [Serializable] private class Report{public int screenWidth,screenHeight;public Rect documentBounds;public List<Item> items=new List<Item>();}
    public void Execute(ExecutionResult result)
    {
        var hud=UnityEngine.Object.FindFirstObjectByType<EarthDuelHud>();
        if(hud==null)throw new Exception("Actual active EarthDuelHud required.");
        var root=hud.GetComponent<UIDocument>().rootVisualElement;
        var report=new Report{screenWidth=Screen.width,screenHeight=Screen.height,documentBounds=root.worldBound};
        if(root.worldBound.width<=0||root.worldBound.height<=0)throw new Exception("Document has not completed layout.");
        string[] names={"reference-hud-wordmark","duel-scoreboard","round-clock","health-gauge","mana-gauge","health-value","energy-value","pause-match","reference-exact-element-row","reference-token-Fire","reference-token-Earth","reference-token-Water","reference-token-Air","planet-globe","reference-exact-orbit-frame","orbit-caption","round-result","restart-round","reference-result-menu"};
        foreach(string name in names)
        {
            var e=root.Q(name);if(e==null)throw new Exception("Missing expected node: "+name);Rect b=e.worldBound;
            report.items.Add(new Item{name=name,text=(e as TextElement)?.text,panelBounds=b,normalizedBounds=new Rect((b.x-root.worldBound.x)/root.worldBound.width,(b.y-root.worldBound.y)/root.worldBound.height,b.width/root.worldBound.width,b.height/root.worldBound.height),display=e.resolvedStyle.display.ToString(),visibility=e.resolvedStyle.visibility.ToString(),picking=e.pickingMode.ToString(),fontSize=e.resolvedStyle.fontSize});
        }
        Directory.CreateDirectory("BuildReports/ReferenceHudExact");
        string path="BuildReports/ReferenceHudExact/actual-bounds-"+Screen.width+"x"+Screen.height+".json";
        File.WriteAllText(path,JsonUtility.ToJson(report,true));result.Log("Recorded actual HUD bounds to {0}",path);
    }
}
