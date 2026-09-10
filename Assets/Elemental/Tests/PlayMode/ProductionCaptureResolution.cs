using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Elemental.Tests.PlayMode
{
    /// <summary>Fixed render dimensions, never a resized screenshot. Dispose restores only the owned settings.</summary>
    internal sealed class ProductionCaptureResolution : IDisposable
    {
        public const int Width=1920, Height=1080;
        private readonly UniversalRenderPipelineAsset pipeline;
        private readonly float oldScale;
        private readonly int oldWidth=Screen.width,oldHeight=Screen.height;
        private readonly FullScreenMode oldMode=Screen.fullScreenMode;
#if UNITY_EDITOR
        private readonly UnityEditor.EditorWindow window;
        private readonly PropertyInfo selected;
        private readonly int oldIndex;
        private readonly object group;
        private readonly int addedIndex;
#endif
        public ProductionCaptureResolution()
        {
            pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline!=null){oldScale=pipeline.renderScale;pipeline.renderScale=1;}
#if UNITY_EDITOR
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes=singleton.GetProperty("instance").GetValue(null);
            var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
            group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
            var viewType=assembly.GetType("UnityEditor.GameView");
            window=UnityEditor.EditorWindow.GetWindow(viewType);
            selected=viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            oldIndex=(int)selected.GetValue(window);
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kindType=assembly.GetType("UnityEditor.GameViewSizeType");
            var constructor=sizeType.GetConstructor(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,
                new[]{kindType,typeof(int),typeof(int),typeof(string)},null);
            object item=constructor.Invoke(new[]{Enum.ToObject(kindType,1),(object)Width,Height,"Hard Polish capture 1920x1080"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{item});
            addedIndex=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
            selected.SetValue(window,addedIndex);window.Focus();window.Repaint();
#else
            Screen.SetResolution(Width,Height,FullScreenMode.Windowed);
#endif
        }
        public IEnumerator WaitForRenderedSize(Camera camera)
        {
            Assert.That(camera,Is.Not.Null,"Bind the existing Celestial target/output camera.");
            double until=Time.realtimeSinceStartupAsDouble+10;
            while((Screen.width!=Width||Screen.height!=Height||camera.pixelWidth!=Width||camera.pixelHeight!=Height)&&Time.realtimeSinceStartupAsDouble<until)
                yield return null;
            Assert.That(Screen.width,Is.EqualTo(Width));Assert.That(Screen.height,Is.EqualTo(Height));
            Assert.That(camera.pixelWidth,Is.EqualTo(Width));Assert.That(camera.pixelHeight,Is.EqualTo(Height));
            Assert.That(camera.allowDynamicResolution,Is.False,"Capture must use actual native render pixels.");
            yield return new WaitForEndOfFrame();
        }
        // Call after WaitForEndOfFrame: includes the actual GameView's UI and active render pipeline.
        public static void SaveScreen(string path)
        {
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(image.width,Is.EqualTo(Width));Assert.That(image.height,Is.EqualTo(Height));
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{UnityEngine.Object.Destroy(image);}
        }
        public void Dispose()
        {
#if UNITY_EDITOR
            if(window!=null)
            {
                selected.SetValue(window,oldIndex);
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{addedIndex});
                window.Repaint();
            }
#else
            Screen.SetResolution(oldWidth,oldHeight,oldMode);
#endif
            if(pipeline!=null)pipeline.renderScale=oldScale;
        }
    }
}
