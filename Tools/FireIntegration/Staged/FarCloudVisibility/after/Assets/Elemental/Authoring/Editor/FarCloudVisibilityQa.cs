using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Elemental.Presentation.Rendering;
namespace Elemental.Authoring.Editor
{
 public static class FarCloudVisibilityQa
 {
  [Serializable] private sealed class Evidence
  {
   public string utc;public int width=1920,height=1080,farMaskPixels,farChangedPixels,cloudChangedPixels;
   public float farMeanAbsoluteRgb,cloudMeanAbsoluteRgb;public Vector3 cameraPosition,cameraForward;
  }
  [MenuItem("Elemental/Graphics/Capture Far Art And Procedural Clouds A B")]
  public static void Capture()
  {
   if(!Application.isPlaying)throw new InvalidOperationException("Use the production Main or Combat Play camera.");
   ValleyAtmosphereController atmosphere=null;ProceduralCloudBanks banks=null;Camera camera=null;
   foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
   {atmosphere=root.GetComponentInChildren<ValleyAtmosphereController>(true)??atmosphere;banks=root.GetComponentInChildren<ProceduralCloudBanks>(true)??banks;}
   foreach(var candidate in Camera.allCameras)if(candidate.enabled && candidate.cameraType==CameraType.Game && (camera==null || candidate.depth<camera.depth))camera=candidate;
   if(atmosphere==null || banks==null || camera==null)throw new InvalidOperationException("Missing atmosphere, procedural banks or active game camera.");
   bool oldFar=atmosphere.FarArtEnabled,oldCloud=banks.gameObject.activeSelf;int oldDebug=atmosphere.DebugMode;
   var target=camera.targetTexture;var active=RenderTexture.active;
   var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);rt.Create();
   var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
   string folder="Logs/FarCloudVisibility/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss");Directory.CreateDirectory(folder);
   var evidence=new Evidence{utc=DateTime.UtcNow.ToString("O"),cameraPosition=camera.transform.position,cameraForward=camera.transform.forward};
   try
   {
    camera.targetTexture=rt;atmosphere.DebugMode=0;atmosphere.FarArtEnabled=false;banks.gameObject.SetActive(false);
    Color32[] baseline=Shot(camera,atmosphere,rt,texture,folder+"/off.png");
    atmosphere.FarArtEnabled=true;Color32[] far=Shot(camera,atmosphere,rt,texture,folder+"/far-on.png");
    Compare(baseline,far,out evidence.farChangedPixels,out evidence.farMeanAbsoluteRgb);
    atmosphere.DebugMode=3;Color32[] mask=Shot(camera,atmosphere,rt,texture,folder+"/far-mask.png");
    for(int i=0;i<mask.Length;i++)if(mask[i].r>2)evidence.farMaskPixels++;
    atmosphere.DebugMode=0;atmosphere.FarArtEnabled=false;banks.gameObject.SetActive(true);
    Color32[] clouds=Shot(camera,atmosphere,rt,texture,folder+"/procedural-on.png");
    Compare(baseline,clouds,out evidence.cloudChangedPixels,out evidence.cloudMeanAbsoluteRgb);
    File.WriteAllText(folder+"/evidence.json",JsonUtility.ToJson(evidence,true));Debug.Log("Far/cloud A B: "+folder+" farPixels="+evidence.farChangedPixels+" cloudPixels="+evidence.cloudChangedPixels);
   }
   finally
   {
    atmosphere.FarArtEnabled=oldFar;atmosphere.DebugMode=oldDebug;banks.gameObject.SetActive(oldCloud);atmosphere.Publish();
    camera.targetTexture=target;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
   }
  }
  private static Color32[] Shot(Camera camera,ValleyAtmosphereController atmosphere,RenderTexture rt,Texture2D texture,string file)
  {atmosphere.Publish();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(file,texture.EncodeToPNG());return texture.GetPixels32();}
  private static void Compare(Color32[] a,Color32[] b,out int changed,out float mean)
  {changed=0;double sum=0;for(int i=0;i<a.Length;i++){int delta=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);if(delta>0)changed++;sum+=delta;}mean=(float)(sum/(a.Length*3.0*255));}
 }
}
