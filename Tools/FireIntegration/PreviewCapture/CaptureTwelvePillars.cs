// Replay this entire file as Unity_RunCommand.Code after Preview Twelve Pillars.
// This is Tools-only command source, not a Unity Assets script.
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
        Transform preview=null;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var owner in root.GetComponentsInChildren<DistantBackdrop>(true))
            {
                Transform candidate=owner.transform.Find("EE_RockPreview_V2");
                if(candidate==null)continue;
                if(preview!=null)throw new Exception("Multiple preview owners.");
                preview=candidate;
            }
        if(preview==null)throw new Exception("Run Preview Twelve Pillars first.");
        var items=preview.GetComponentsInChildren<MeshRenderer>();
        if(items.Length!=12)throw new Exception("Expected exactly twelve preview pillars.");
        int[] layers=new int[items.Length];
        var cameraObject=new GameObject("Temporary Rock QA Camera");
        result.RegisterObjectCreation(cameraObject);
        var camera=cameraObject.AddComponent<Camera>();
        camera.enabled=false;
        // Existing AtmosphereFullscreenFeature enqueues only CameraType.Game.
        // This skips its fog without changing global atmosphere or any renderer asset.
        camera.cameraType=CameraType.Preview;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.22f,.24f,.28f);
        camera.orthographic=true;camera.aspect=1.6f;
        camera.nearClipPlane=.1f;camera.farClipPlane=100;
        camera.cullingMask=1<<30;
        var data=camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing=false;data.renderShadows=true;
        var target=new RenderTexture(1920,1200,24,RenderTextureFormat.ARGB32);
        var texture=new Texture2D(1920,1200,TextureFormat.RGB24,false);
        var prior=RenderTexture.active;
        try
        {
            for(int i=0;i<items.Length;i++){layers[i]=items[i].gameObject.layer;items[i].gameObject.layer=30;}
            Directory.CreateDirectory("BuildReports/ProceduralValleyV2");
            camera.orthographicSize=9;
            Vector3 center=preview.TransformPoint(new Vector3(4.2f,2,5.5f));
            camera.transform.position=center+preview.up*12-preview.forward*18+preview.right*13;
            camera.transform.rotation=Quaternion.LookRotation(center-camera.transform.position,preview.up);
            Capture(camera,target,texture,"03-refined-twelve-no-fog");
            // Isolate one object for the close shot, then its underside. No neighbors
            // or nearby arena objects can obscure the actual generated silhouette.
            for(int i=1;i<items.Length;i++)items[i].gameObject.layer=0;
            camera.orthographicSize=3.1f;center=items[0].bounds.center;
            camera.transform.position=center+preview.up*2.1f-preview.forward*6+preview.right*4;
            camera.transform.rotation=Quaternion.LookRotation(center-camera.transform.position,preview.up);
            Capture(camera,target,texture,"04-refined-pillar-close-no-fog");
            camera.transform.position=center-preview.up*3.2f-preview.forward*6+preview.right*4;
            camera.transform.rotation=Quaternion.LookRotation(center-camera.transform.position,preview.up);
            Capture(camera,target,texture,"05-refined-pillar-underside-no-fog");
            result.Log("Captured12 existing-material pillars, isolated close and underside. Production camera/light untouched. Clear Preview Only before running tests that may save scenes.");
        }
        finally
        {
            for(int i=0;i<items.Length;i++)if(items[i]!=null)items[i].gameObject.layer=layers[i];
            RenderTexture.active=prior;target.Release();
            result.DestroyObject(texture);result.DestroyObject(target);result.DestroyObject(cameraObject);
        }
    }
    private static void Capture(Camera camera,RenderTexture target,Texture2D texture,string name)
    {
        var request=new RenderPipeline.StandardRequest{destination=target};
        if(!RenderPipeline.SupportsRenderRequest(camera,request))throw new Exception("Current renderer cannot submit this preview request.");
        RenderPipeline.SubmitRenderRequest(camera,request);
        RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1920,1200),0,0);texture.Apply();
        File.WriteAllBytes("BuildReports/ProceduralValleyV2/"+name+".png",texture.EncodeToPNG());
    }
}
