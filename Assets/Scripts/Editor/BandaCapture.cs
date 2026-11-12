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

        /// <summary>Два PNG однакової висоти поруч (ліворуч | праворуч) з темною смугою між ними.</summary>
        public static string SideBySide(string leftPath, string rightPath, string outPath)
        {
            Texture2D left = Load(leftPath);
            Texture2D right = Load(rightPath);
            const int gap = 8;
            int height = Mathf.Max(left.height, right.height);
            Texture2D result = new Texture2D(left.width + gap + right.width, height, TextureFormat.RGB24, false);
            Color[] fill = new Color[result.width * result.height];
            for (int i = 0; i < fill.Length; i++)
            {
                fill[i] = Color.black;
            }

            result.SetPixels(fill);
            result.SetPixels(0, height - left.height, left.width, left.height, left.GetPixels());
            result.SetPixels(left.width + gap, height - right.height, right.width, right.height, right.GetPixels());
            result.Apply();
            File.WriteAllBytes(outPath, result.EncodeToPNG());
            Object.DestroyImmediate(left);
            Object.DestroyImmediate(right);
            Object.DestroyImmediate(result);
            return outPath;
        }

        /// <summary>Зменшена копія (bilinear) — «як на телефоні» на відстані витягнутої руки.</summary>
        public static string Downscale(string sourcePath, string outPath, float factor)
        {
            Texture2D source = Load(sourcePath);
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * factor));
            int h = Mathf.Max(1, Mathf.RoundToInt(source.height * factor));
            Texture2D result = new Texture2D(w, h, TextureFormat.RGB24, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    result.SetPixel(x, y, source.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h));
                }
            }

            result.Apply();
            File.WriteAllBytes(outPath, result.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
            return outPath;
        }

        /// <summary>Середній колір прямокутника (координати від верхнього лівого кута) — для звірки з референсом.</summary>
        public static Color AverageColor(string path, RectInt region)
        {
            Texture2D texture = Load(path);
            Color sum = Color.clear;
            int count = 0;
            for (int y = region.yMin; y < region.yMax; y += 2)
            {
                for (int x = region.xMin; x < region.xMax; x += 2)
                {
                    sum += texture.GetPixel(x, texture.height - 1 - y);
                    count++;
                }
            }

            Object.DestroyImmediate(texture);
            return count > 0 ? sum / count : Color.clear;
        }

        private static Texture2D Load(string path)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }
    }
}
