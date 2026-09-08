// Tools-only replay through Unity_RunCommand in Main, then again in Combat.
// Read-only scene inspection; writes a QA JSON report, no camera/scene mutation.
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Elemental.Presentation.DistantScenery;
using Elemental.Presentation.UI;
internal class CommandScript:IRunCommand
{
    [Serializable] internal class Item{public string name;public Vector2 imageMin,imageMax;public float minimumDepth;}
    [Serializable] internal class Report{public string state;public Vector3 cameraPosition,cameraForward,cameraRight,cameraUp;public float aspect,fieldOfView;public List<Item> items=new List<Item>();}
    public void Execute(ExecutionResult result)
    {
        UnityEngine.Camera camera=null;DistantBackdrop backdrop=null;FrontendFlowController flow=null;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach(var c in root.GetComponentsInChildren<UnityEngine.Camera>(true))if(c.name=="Gravity Toy Camera")camera=c;
            foreach(var owner in root.GetComponentsInChildren<DistantBackdrop>(true))backdrop=owner;
            foreach(var owner in root.GetComponentsInChildren<FrontendFlowController>(true))flow=owner;
        }
        if(camera==null||backdrop==null||flow==null)throw new Exception("Production camera/backdrop/frontend required.");
        var report=new Report{state=flow.State.ToString(),cameraPosition=camera.transform.position,cameraForward=camera.transform.forward,cameraRight=camera.transform.right,cameraUp=camera.transform.up,aspect=camera.aspect,fieldOfView=camera.fieldOfView};
        foreach(var renderer in backdrop.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(renderer.name!="LOD0")continue;
            var parent=renderer.transform.parent;if(parent==null||!parent.name.StartsWith("View_"))continue;
            Bounds b=renderer.bounds;var item=new Item{name=parent.name,imageMin=new Vector2(float.PositiveInfinity,float.PositiveInfinity),imageMax=new Vector2(float.NegativeInfinity,float.NegativeInfinity),minimumDepth=float.PositiveInfinity};
            for(int i=0;i<8;i++)
            {
                var world=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 p=camera.WorldToViewportPoint(world);Vector2 image=new Vector2(p.x,1-p.y);
                item.imageMin=Vector2.Min(item.imageMin,image);item.imageMax=Vector2.Max(item.imageMax,image);item.minimumDepth=Mathf.Min(item.minimumDepth,p.z);
            }
            report.items.Add(item);
        }
        Directory.CreateDirectory("BuildReports/IslandCoverageComposition");
        string path="BuildReports/IslandCoverageComposition/"+report.state+"-projected-bounds.json";
        File.WriteAllText(path,JsonUtility.ToJson(report,true));result.Log("Recorded {0} conservative renderer bounds using actual camera to {1}.",report.items.Count,path);
    }
}
