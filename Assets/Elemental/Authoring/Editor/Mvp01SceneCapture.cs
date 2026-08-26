using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Elemental.Authoring.Editor
{
    /// <summary>
    /// Captures the actual game camera without depending on the Editor window compositor.
    /// This is useful on DX11 machines where desktop automation can see a blank Game View.
    /// </summary>
    public static class Mvp01SceneCapture
    {
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Elemental/QA/Capture M3 Game Camera")]
        public static void Capture()
        {
            Camera camera = Camera.main != null
                ? Camera.main
                : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
                throw new InvalidOperationException("The active scene does not contain a camera.");

            string fileName = Application.isPlaying
                ? "Mvp01CombatLatest.png"
                : "Mvp01SceneLatest.png";
            string path = Path.GetFullPath(Path.Combine("BuildReports", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "MVP 0.1 QA Capture"
            };
            Texture2D pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log($"[Elemental] MVP 0.1 camera captured: {path}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
