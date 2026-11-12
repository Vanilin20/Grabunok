using System.IO;
using UnityEngine;

namespace Banda.EditorTools
{
    /// <summary>
    /// Знімок кадру гри разом з інтерфейсом у PNG заданого розміру (для циклу якості й порівнянь).
    /// Працює і тоді, коли вікно редактора без фокусу й Game view не оновлюється:
    /// на час знімка Canvas переводиться в режим Screen Space - Camera і рендериться в текстуру.
    /// </summary>
    public static class BandaCapture
    {
        public static string Capture(string path, int width, int height)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return "немає Main Camera";
            }

            GameObject canvasGo = GameObject.Find("UiCanvas");
            Canvas canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
            RenderTexture rt = new RenderTexture(width, height, 24);
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = rt;

            RenderMode previousMode = RenderMode.ScreenSpaceOverlay;
            string previousLayer = "Default";
            int previousOrder = 0;
            if (canvas != null)
            {
                previousMode = canvas.renderMode;
                previousLayer = canvas.sortingLayerName;
                previousOrder = canvas.sortingOrder;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5f;
                // Інтерфейс поверх усього світу, як в оверлейному режимі.
                canvas.sortingLayerName = "Front";
                canvas.sortingOrder = 1000;
            }

            Canvas.ForceUpdateCanvases();
            camera.Render();
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture.active = rt;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
            if (canvas != null)
            {
                canvas.renderMode = previousMode;
                canvas.sortingLayerName = previousLayer;
                canvas.sortingOrder = previousOrder;
                Canvas.ForceUpdateCanvases();
            }

            return path;
        }
    }
}
