// Replay only AFTER root accepts the twelve-pillar preview. No asset-bank bake.
using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Elemental.Presentation.DistantScenery;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        DistantBackdrop owner=null;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var candidate in root.GetComponentsInChildren<DistantBackdrop>(true))
            {if(owner!=null)throw new Exception("Multiple backdrop owners.");owner=candidate;}
        if(owner==null || owner.profile==null || owner.profile.material==null)throw new Exception("Existing backdrop material/profile required.");
        var preview=new GameObject("Temporary Two Rock Groups QA");result.RegisterObjectCreation(preview);
        var cameraObject=new GameObject("Temporary Group QA Camera");result.RegisterObjectCreation(cameraObject);
        UnityEngine.Mesh ground=null,floating=null;RenderTexture target=null;Texture2D texture=null;
        var prior=RenderTexture.active;
        try
        {
            Vector3 up=owner.stagingUp.normalized,forward=Vector3.ProjectOnPlane(owner.profile.heroViewDirection,up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.forward+Vector3.right,up).normalized;
            Vector3 right=Vector3.Cross(up,forward).normalized;
            preview.transform.SetPositionAndRotation(owner.planetCenter.position+right*(owner.profile.planetRadius+80)+up*owner.profile.planetRadius,Quaternion.LookRotation(forward,up));
            ground=ProceduralRockMesh.Group(owner.profile.geometrySeed,5,false,0,false,owner.profile.rockShape);
            floating=ProceduralRockMesh.Group(unchecked(owner.profile.geometrySeed+7*131),4,true,1,false,owner.profile.rockShape);
            var objects=new[]{new GameObject("Ground Group"),new GameObject("Floating Group")};
            for(int i=0;i<objects.Length;i++)
            {
                var go=objects[i];go.transform.SetParent(preview.transform,false);go.layer=30;
                go.transform.localScale=Vector3.one*5;go.transform.localPosition=new Vector3(i*7,0,0);
                go.AddComponent<MeshFilter>().sharedMesh=i==0?ground:floating;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=owner.profile.material;renderer.receiveShadows=true;
            }
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cameraType=CameraType.Preview;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.24f,.28f);
            camera.orthographic=true;camera.aspect=1.6f;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.cullingMask=1<<30;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.GetUniversalAdditionalCameraData().renderShadows=true;
            target=new RenderTexture(1920,1200,24,RenderTextureFormat.ARGB32);texture=new Texture2D(1920,1200,TextureFormat.RGB24,false);
            Directory.CreateDirectory("BuildReports/ProceduralValleyV2");
            for(int i=0;i<2;i++)for(int view=0;view<2;view++)
            {
                objects[1-i].SetActive(false);objects[i].SetActive(true);
                Bounds bounds=objects[i].GetComponent<MeshRenderer>().bounds;
                camera.orthographicSize=3.8f;
                camera.transform.position=bounds.center+up*(view==0?3:-3)-forward*7+right*5;
                camera.transform.rotation=Quaternion.LookRotation(bounds.center-camera.transform.position,up);
                var request=new RenderPipeline.StandardRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new Exception("Preview render request unsupported.");
                RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1920,1200),0,0);texture.Apply();
                File.WriteAllBytes("BuildReports/ProceduralValleyV2/"+(i==0?"06-ground":"07-floating")+(view==0?"-front":"-underside")+".png",texture.EncodeToPNG());
            }
            File.WriteAllText("BuildReports/ProceduralValleyV2/two-groups-counts.json","{\"groundTriangles\":"+ground.triangles.Length/3+",\"floatingTriangles\":"+floating.triangles.Length/3+"}");
            result.Log("Capturedone ground andone floating group; transient meshes/objects removed, no scene save or bank bake.");
        }
        finally
        {
            RenderTexture.active=prior;if(target!=null){target.Release();result.DestroyObject(target);}if(texture!=null)result.DestroyObject(texture);
            result.DestroyObject(cameraObject);result.DestroyObject(preview);
            if(ground!=null)result.DestroyObject(ground);if(floating!=null)result.DestroyObject(floating);
        }
    }
}
