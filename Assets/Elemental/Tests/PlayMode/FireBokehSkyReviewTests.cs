#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Elemental.Presentation.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
namespace Elemental.Tests.PlayMode
{
 public sealed partial class HardPolishFireStreamBindingRuntimeTests
 {
  [UnityTest,Timeout(180000)]public IEnumerator SavedSkyIslandEdgesWithActualBokehAndAtmosphere()
  {
   float saved=Time.captureDeltaTime;FireVisualCaptureCamera capture=null;System.IDisposable isolation=null;
   ScriptableRendererFeature bokeh=null;bool wasActive=false;EarthCinematicDepthOfFieldController dof=null;bool captureWas=false;EarthCinematicDepthOfFieldDebugView debugWas=default;
   Transform primaryWas=null,secondaryWas=null;float weightWas=1;
   try
   {
    Time.captureDeltaTime=1f/60;yield return ReadyFireAbilities();isolation=new FireVisualRivalIsolation(duel.BotTransform,flightMotor);
    var view=All<CelestialSystemBehaviour>().Single().TargetCamera;var data=view.GetUniversalAdditionalCameraData();
    var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;var serialized=new SerializedObject(pipeline);
    int index=new SerializedObject(data).FindProperty("m_RendererIndex").intValue;if(index<0)index=serialized.FindProperty("m_DefaultRendererIndex").intValue;
    var rendererData=(ScriptableRendererData)serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(index).objectReferenceValue;
    bokeh=rendererData.rendererFeatures.Single(f=>f is EarthCinematicDepthOfFieldFeature);wasActive=bokeh.isActive;
    dof=view.GetComponent<EarthCinematicDepthOfFieldController>();captureWas=dof.HasCaptureOverride;debugWas=dof.CaptureDebugView;
    primaryWas=dof.PrimarySubject;secondaryWas=dof.SecondarySubject;weightWas=dof.PresentationWeight;
    // Rival isolation deliberately parks the opponent outside this view. Focus
    // on the actual visible fighter; an invalid off-camera second subject disables DOF.
    dof.ConfigureSubjects(duel.PlayerTransform,duel.PlayerTransform);dof.SetPresentationWeight(1);dof.SetCaptureOverride(true);
    capture=view.gameObject.AddComponent<FireVisualCaptureCamera>();Vector3 up=flightMotor.LocalUp,forward=flightMotor.FacingForward,side=Vector3.Cross(up,forward).normalized,center=flightMotor.Body.position+up;
    string folder="BuildReports/HardPolish/G05/SkyBokeh-"+System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);
    for(int angle=0;angle<2;angle++)
    {
     capture.Place(center-forward*6+side*angle*3+up,center+forward*4);
     foreach(bool enabled in new[]{false,true})
     {
      bokeh.SetActive(enabled);yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();Assert.That(dof.TryGetRenderSettings(out var settings),Is.True);
      var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/view-"+angle+(enabled?"-bokeh-on":"-bokeh-off")+".png",image.EncodeToPNG());}finally{Object.Destroy(image);}
      File.WriteAllText(folder+"/focus.txt","Actual project DOF radius="+settings.MaxRadiusPixels+" near="+settings.SharpNearDistance+" far="+settings.SharpFarDistance+". Compare actual island silhouettes; this capture is not an automatic art acceptance.");
     }
    }
   }
   finally{if(bokeh!=null)bokeh.SetActive(wasActive);if(dof!=null){dof.ConfigureSubjects(primaryWas,secondaryWas);dof.SetPresentationWeight(weightWas);dof.SetCaptureOverride(captureWas,debugWas);}if(capture!=null)Object.Destroy(capture);isolation?.Dispose();Time.captureDeltaTime=saved;ReleaseFireAbilityFixture();}
  }
 }
}
#endif
