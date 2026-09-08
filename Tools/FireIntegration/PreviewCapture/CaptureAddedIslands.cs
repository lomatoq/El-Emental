using UnityEngine;
using System.IO;
using Elemental.Presentation.DistantScenery;
internal class CommandScript : IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  if(!Application.isPlaying)throw new System.Exception("Play camera required");
  DistantBackdrop backdrop=null;Camera camera=null;
  foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())backdrop=root.GetComponentInChildren<DistantBackdrop>(true)??backdrop;
  foreach(var c in Camera.allCameras)if(c.enabled&&c.cameraType==CameraType.Game&&(camera==null||c.depth<camera.depth))camera=c;
  if(backdrop==null||camera==null)throw new System.Exception("Missing production camera or backdrop");
  var position=camera.transform.position;var rotation=camera.transform.rotation;var target=camera.targetTexture;var active=RenderTexture.active;float fov=camera.fieldOfView,aspect=camera.aspect;
  var rt=new RenderTexture(1920,1080,24);rt.Create();var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
  Directory.CreateDirectory("BuildReports/IslandPerpendicularOverhead");
  try
  {
   camera.targetTexture=rt;camera.fieldOfView=60;camera.aspect=1920f/1080;
   camera.transform.position=backdrop.planetCenter.position+Vector3.up*59;
   for(int i=0;i<3;i++)
   {
    string name=i==0?"East":i==1?"West":"Overhead";
    camera.transform.rotation=Quaternion.LookRotation(i==2?Vector3.up:new Vector3(i==0?1:-1,-.08f,0),i==2?Vector3.forward:Vector3.up);
    camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
    File.WriteAllBytes("BuildReports/IslandPerpendicularOverhead/"+name+".png",pixels.EncodeToPNG());
   }
  }
  finally
  {
   camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.aspect=aspect;camera.targetTexture=target;RenderTexture.active=active;
   Object.Destroy(pixels);rt.Release();Object.Destroy(rt);
  }
  result.Log("Captured three actual island directions; production camera restored.");
 }
}
