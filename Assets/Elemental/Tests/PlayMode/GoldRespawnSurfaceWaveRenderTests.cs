using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace Elemental.Tests.PlayMode
{
    public sealed class GoldRespawnSurfaceWaveRenderTests
    {
        [UnityTest] public IEnumerator ActualRockShaderWaveFollowsRaisedSurfaceExcludesCharacterAndMovesInward()
        {
            int centersId = Shader.PropertyToID("_GoldRespawnWaveCenters"), upsId = Shader.PropertyToID("_GoldRespawnWaveUps");
            var oldCenters = Shader.GetGlobalVectorArray(centersId); var oldUps = Shader.GetGlobalVectorArray(upsId);
            bool oldFog = RenderSettings.fog;
            var root = new GameObject("Isolated actual-shader terrain-wave proof");
            var material = new Material(Shader.Find("Elemental/Graphics V5/Rumble Rock Lit"));
            var character = new Material(material); character.SetFloat("_SurfaceMode", 1);
            material.SetFloat("_SurfaceMode", 0);
            var mesh = new Mesh { vertices = new[]{new Vector3(-.15f,0,-.15f),new Vector3(-.15f,0,.15f),new Vector3(.15f,0,.15f),new Vector3(.15f,0,-.15f)}, triangles = new[]{0,1,2,0,2,3}, normals = new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up} };
            mesh.RecalculateBounds();
            Vector3[] points = { new(-.5f,0,0), new(-2,.5f,1.5f), new(-2,1.5f,-1.5f), new(-3.5f,0,0), new(3.5f,0,0), new(-1.5f,0,0) };
            var cameraObject = new GameObject("Wave proof camera"); cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled=false; camera.orthographic=true;camera.orthographicSize=4.5f;camera.aspect=1;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.transform.SetPositionAndRotation(new Vector3(0,10,0),Quaternion.Euler(90,0,0));
            var target = new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf);target.Create();
            var read = new Texture2D(512,512,TextureFormat.RGBAFloat,false,true);
            var zero = new Vector4[2];
            Color[] Capture(string name)
            {
                var request = new RenderPipeline.StandardRequest { destination=target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
                RenderPipeline.SubmitRenderRequest(camera,request);
                var previous=RenderTexture.active;
                try { RenderTexture.active=target;read.ReadPixels(new Rect(0,0,512,512),0,0);read.Apply(); }
                finally { RenderTexture.active=previous; }
                Directory.CreateDirectory("BuildReports/HardPolish/G07/SurfaceWave");
                                var png = new Texture2D(512,512,TextureFormat.RGBA32,false);
                try { png.SetPixels(read.GetPixels()); png.Apply(); File.WriteAllBytes("BuildReports/HardPolish/G07/SurfaceWave/"+name+".png",png.EncodeToPNG()); }
                finally { Object.Destroy(png); }
                var samples=new Color[points.Length];
                for(int i=0;i<points.Length;i++){Vector3 viewport=camera.WorldToViewportPoint(points[i]);samples[i]=read.GetPixel(Mathf.RoundToInt(viewport.x*511),Mathf.RoundToInt(viewport.y*511));}
                return samples;
            }
            try
            {
                RenderSettings.fog=false;
                for(int i=0;i<points.Length;i++)
                {var node=new GameObject("Surface "+i);node.layer=31;node.transform.SetParent(root.transform);node.transform.position=points[i];node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>().sharedMaterial=i==3?character:material;}
                Shader.SetGlobalVectorArray(centersId,zero);Shader.SetGlobalVectorArray(upsId,zero);
                yield return null;
                Color[] baseline=Capture("00-baseline");
                Shader.SetGlobalVectorArray(centersId,new[]{new Vector4(-2,0,0,1.5f),new Vector4(2,0,0,1.5f)});
                Shader.SetGlobalVectorArray(upsId,new[]{new Vector4(0,1,0,1),new Vector4(0,1,0,1)});
                Color[] active=Capture("01-two-cues-raised-terrain");
                foreach(int i in new[]{0,1,4}) Assert.That(active[i].r-baseline[i].r,Is.GreaterThan(.2f),"Real shader emission must reach ground, raised terrain and the second actor cue.");
                foreach(int i in new[]{2,3,5}) Assert.That(Mathf.Abs(active[i].r-baseline[i].r),Is.LessThan(.005f),"Height bound, character exclusion and off-band geometry must remain unchanged.");
                Shader.SetGlobalVectorArray(centersId,new[]{new Vector4(-2,0,0,.5f),Vector4.zero});
                Color[] inward=Capture("02-inward");
                Assert.That(inward[5].r-baseline[5].r,Is.GreaterThan(.2f));
                Assert.That(Mathf.Abs(inward[0].r-baseline[0].r),Is.LessThan(.005f));
                Shader.SetGlobalVectorArray(centersId,zero);Shader.SetGlobalVectorArray(upsId,zero);
                Color[] cleared=Capture("03-cleared");
                for(int i=0;i<points.Length;i++)Assert.That(Mathf.Abs(cleared[i].r-baseline[i].r),Is.LessThan(.005f));
            }
            finally
            {
                Shader.SetGlobalVectorArray(centersId,oldCenters != null && oldCenters.Length==2?oldCenters:zero);Shader.SetGlobalVectorArray(upsId,oldUps != null && oldUps.Length==2?oldUps:zero);
                RenderSettings.fog=oldFog;Object.Destroy(root);Object.Destroy(mesh);Object.Destroy(material);Object.Destroy(character);Object.Destroy(read);target.Release();Object.Destroy(target);
            }
        }
    }
}
