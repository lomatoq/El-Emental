// Tools-only read-only renderer projections. Does not move a production camera.
using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using Elemental.Presentation.DistantScenery;
internal class CommandScript:IRunCommand
{
 public void Execute(ExecutionResult result)
 {
  DistantBackdrop backdrop=null;
  foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
   foreach(var item in root.GetComponentsInChildren<DistantBackdrop>(true))backdrop=item;
  if(backdrop==null)throw new Exception("DistantBackdrop required.");
  var csv=new StringBuilder("view,name,minX,minY,maxX,maxY,minDepth,worldUpDot\n");int count=0;
  Vector3 position=backdrop.planetCenter.position+Vector3.up*59;
  foreach(var renderer in backdrop.GetComponentsInChildren<MeshRenderer>(true))
  {
   string name=renderer.transform.parent.name;
   if(renderer.name!="LOD0"||!(name.StartsWith("View_East")||name.StartsWith("View_West")||name.StartsWith("View_Overhead")))continue;
   bool overhead=name.StartsWith("View_Overhead");string view=overhead?"Overhead":name.StartsWith("View_East")?"East":"West";
   Vector3 forward=overhead?Vector3.up:new Vector3(view=="East"?1:-1,-.08f,0).normalized;
   Vector3 right=Vector3.Cross(overhead?Vector3.forward:Vector3.up,forward).normalized,up=Vector3.Cross(forward,right);
   Bounds bounds=renderer.bounds;Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;float depth=float.PositiveInfinity;
   for(int i=0;i<8;i++)
   {
    Vector3 d=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))-position;
    float z=Vector3.Dot(d,forward);depth=Mathf.Min(depth,z);
    Vector2 uv=new Vector2(.5f+Vector3.Dot(d,right)/(2*.577350269f*(1676f/776)*z),.5f-Vector3.Dot(d,up)/(2*.577350269f*z));
    min=Vector2.Min(min,uv);max=Vector2.Max(max,uv);
   }
   csv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2:F5},{3:F5},{4:F5},{5:F5},{6:F3},{7:F6}\n",view,name,min.x,min.y,max.x,max.y,depth,Vector3.Dot(renderer.transform.parent.up,Vector3.up));count++;
  }
  Directory.CreateDirectory("BuildReports/IslandPerpendicularOverhead");File.WriteAllText("BuildReports/IslandPerpendicularOverhead/actual-coverage.csv",csv.ToString());
  result.Log("Additional island renderer count={0}, generated={1}, rejected={2}. Expected 12 added roots; read CSV for +/-X and overhead projections.",count,backdrop.GeneratedCount,backdrop.RejectedPlacements);
 }
}
