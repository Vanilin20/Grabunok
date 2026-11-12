using System;
using System.Collections.Generic;

namespace Banda.ArtGen
{
    /// <summary>
    /// Програмний рендер освітлення для превʼю поза Unity (коли редактор недоступний):
    /// колір = база × (ніч + Σ світло зі спадом) + власне світіння. Повторює модель URP 2D Light2D
    /// (Multiply-бленд, глобальне світло + точкові/конусні джерела), щоб арт можна було оцінити до Unity.
    /// Фінальна перевірка все одно — рендер з Unity.
    /// </summary>
    public static class LightPreview
    {
        public sealed class Light
        {
            public float X;
            public float Y;
            public float Radius;
            public Rgba Color;
            public float Intensity;
            /// <summary>Напрям конуса (радіани, Y униз); NaN — кругове світло.</summary>
            public float Direction = float.NaN;
            public float HalfAngleDeg = 30f;
        }

        public static PixelCanvas Render(PixelCanvas albedo, PixelCanvas glow, IList<Light> lights, Rgba night, float nightIntensity)
        {
            int w = albedo.Width;
            int h = albedo.Height;
            PixelCanvas result = new PixelCanvas(w, h);
            float nr = night.R / 255f * nightIntensity;
            float ng = night.G / 255f * nightIntensity;
            float nb = night.B / 255f * nightIntensity;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Rgba a = albedo.Pixels[y * w + x];
                    float lr = nr;
                    float lg = ng;
                    float lb = nb;
                    foreach (Light l in lights)
                    {
                        float dx = x + 0.5f - l.X;
                        float dy = y + 0.5f - l.Y;
                        float d = MathF.Sqrt(dx * dx + dy * dy);
                        if (d >= l.Radius)
                        {
                            continue;
                        }

                        float k = 1f - d / l.Radius;
                        k = k * k * (3f - 2f * k); // м'який спад, як falloff у Light2D
                        if (!float.IsNaN(l.Direction) && d > 1f)
                        {
                            float ang = MathF.Atan2(dy, dx) - l.Direction;
                            while (ang > MathF.PI)
                            {
                                ang -= MathF.PI * 2f;
                            }

                            while (ang < -MathF.PI)
                            {
                                ang += MathF.PI * 2f;
                            }

                            float half = l.HalfAngleDeg * MathF.PI / 180f;
                            float edge = 1f - MathF.Abs(ang) / half;
                            if (edge <= 0f)
                            {
                                continue;
                            }

                            k *= MathF.Min(1f, edge * 3f);
                        }

                        lr += l.Color.R / 255f * l.Intensity * k;
                        lg += l.Color.G / 255f * l.Intensity * k;
                        lb += l.Color.B / 255f * l.Intensity * k;
                    }

                    float r = a.R / 255f * lr;
                    float g = a.G / 255f * lg;
                    float b = a.B / 255f * lb;
                    if (glow != null)
                    {
                        Rgba e = glow.Pixels[y * w + x];
                        float ea = e.A / 255f;
                        r += e.R / 255f * ea;
                        g += e.G / 255f * ea;
                        b += e.B / 255f * ea;
                    }

                    result.Pixels[y * w + x] = new Rgba(ToByte(r), ToByte(g), ToByte(b), 255);
                }
            }

            return result;
        }

        /// <summary>Простий bloom: розмита копія яскравих пікселів додається поверх (як Bloom у Volume).</summary>
        public static void Bloom(PixelCanvas image, float threshold, float intensity, int radius)
        {
            int w = image.Width;
            int h = image.Height;
            float[] bright = new float[w * h * 3];
            for (int i = 0; i < w * h; i++)
            {
                Rgba c = image.Pixels[i];
                float m = Math.Max(c.R, Math.Max(c.G, c.B)) / 255f;
                if (m > threshold)
                {
                    float k = (m - threshold) / (1f - threshold);
                    bright[i * 3] = c.R / 255f * k;
                    bright[i * 3 + 1] = c.G / 255f * k;
                    bright[i * 3 + 2] = c.B / 255f * k;
                }
            }

            float[] tmp = BoxBlur(bright, w, h, radius, true);
            float[] blur = BoxBlur(tmp, w, h, radius, false);
            for (int i = 0; i < w * h; i++)
            {
                Rgba c = image.Pixels[i];
                image.Pixels[i] = new Rgba(
                    ToByte(c.R / 255f + blur[i * 3] * intensity),
                    ToByte(c.G / 255f + blur[i * 3 + 1] * intensity),
                    ToByte(c.B / 255f + blur[i * 3 + 2] * intensity),
                    c.A);
            }
        }

        private static float[] BoxBlur(float[] src, int w, int h, int r, bool horizontal)
        {
            float[] dst = new float[src.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sr = 0f, sg = 0f, sb = 0f;
                    int n = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = horizontal ? x + k : x;
                        int yy = horizontal ? y : y + k;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h)
                        {
                            continue;
                        }

                        int j = (yy * w + xx) * 3;
                        sr += src[j];
                        sg += src[j + 1];
                        sb += src[j + 2];
                        n++;
                    }

                    int i = (y * w + x) * 3;
                    dst[i] = sr / n;
                    dst[i + 1] = sg / n;
                    dst[i + 2] = sb / n;
                }
            }

            return dst;
        }

        /// <summary>Збільшити полотно в ціле число разів (nearest) — «як на екрані телефона».</summary>
        public static PixelCanvas Scale(PixelCanvas src, int k)
        {
            PixelCanvas dst = new PixelCanvas(src.Width * k, src.Height * k);
            for (int y = 0; y < dst.Height; y++)
            {
                for (int x = 0; x < dst.Width; x++)
                {
                    dst.Pixels[y * dst.Width + x] = src.Pixels[(y / k) * src.Width + x / k];
                }
            }

            return dst;
        }

        /// <summary>Зменшити вдвічі усередненням (перевірка читабельності «на витягнутій руці»).</summary>
        public static PixelCanvas Half(PixelCanvas src)
        {
            PixelCanvas dst = new PixelCanvas(src.Width / 2, src.Height / 2);
            for (int y = 0; y < dst.Height; y++)
            {
                for (int x = 0; x < dst.Width; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int j = 0; j < 2; j++)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            Rgba c = src.Get(x * 2 + i, y * 2 + j);
                            r += c.R;
                            g += c.G;
                            b += c.B;
                        }
                    }

                    dst.Pixels[y * dst.Width + x] = new Rgba((byte)(r / 4), (byte)(g / 4), (byte)(b / 4));
                }
            }

            return dst;
        }

        private static byte ToByte(float v) => (byte)Math.Clamp(v * 255f + 0.5f, 0f, 255f);
    }
}
