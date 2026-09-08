using System;
using System.IO;
using System.Text;
using UnityEngine;
using Elemental.Presentation.UI;
internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  FrontendMenuView view=null;
  foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
   view=root.GetComponentInChildren<FrontendMenuView>(true)??view;
  if(view==null)throw new Exception("Production FrontendMenuView is required.");
  var report=new StringBuilder();int tokens=0;
  foreach(var rect in view.GetComponentsInChildren<RectTransform>(true))
  {
   if(rect.name.StartsWith("Element token "))
   {
    tokens++;
    int symbols=0,frames=0;
    foreach(var image in rect.GetComponentsInChildren<UnityEngine.UI.Image>(true))if(image.name=="Element glyph")symbols++;
    foreach(var graphic in rect.GetComponentsInChildren<StoneReferenceGlyph>(true))if(graphic.shape==StoneReferenceGlyph.Shape.SingleDiamond)frames++;
    if(symbols!=1||frames!=1)throw new Exception(rect.name+" duplicate or missing symbol/frame: "+symbols+"/"+frames);
    report.AppendLine(rect.name+" symbols="+symbols+" frames="+frames+" scale="+rect.localScale.x);
   }
   if(rect.name=="Active element ribbon")report.AppendLine("Card anchored="+rect.anchoredPosition+" size="+rect.sizeDelta);
   if(rect.GetComponent<FrontendButton>()!=null)
    report.AppendLine(rect.parent.name+"/"+rect.name+" anchored="+rect.anchoredPosition+" size="+rect.sizeDelta);
  }
  if(tokens!=4)throw new Exception("Expected four sidebar tokens, found "+tokens);
  Directory.CreateDirectory("BuildReports/CombinedVisual");
  File.WriteAllText("BuildReports/CombinedVisual/SidebarRuntime.txt",report.ToString());
  Debug.Log(report.ToString());
 }
}
