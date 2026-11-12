using System;
using System.IO;
using UnityEngine;

namespace Banda.ArtGen
{
    /// <summary>Колір 8 біт на канал (без залежності від UnityEngine.Color32).</summary>
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public readonly byte A;

        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);

        public Rgba(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Rgba Hex(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255);
        }

        public Rgba WithAlpha(byte a) => new Rgba(R, G, B, a);

        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Rgba(
                (byte)(a.R + (b.R - a.R) * t + 0.5f),
                (byte)(a.G + (b.G - a.G) * t + 0.5f),
                (byte)(a.B + (b.B - a.B) * t + 0.5f),
                (byte)(a.A + (b.A - a.A) * t + 0.5f));
        }

        public bool Equals(Rgba o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object obj) => obj is Rgba o && Equals(o);
        public override int GetHashCode() => (R << 24) | (G << 16) | (B << 8) | A;
    }

    /// <summary>
    /// Піксельне полотно для генераторів арту: усе малюється цілими пікселями, без згладжування.
    /// Y — униз (як у PNG і макетах).
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Rgba[] Pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Rgba[width * height];
        }

        /// <summary>Прочитати PNG через Texture2D.LoadImage (рядки Unity — знизу, полотно — згори).</summary>
        public static PixelCanvas Load(string path)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            PixelCanvas canvas = new PixelCanvas(texture.width, texture.height);
            Color32[] src = texture.GetPixels32();
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    Color32 c = src[(canvas.Height - 1 - y) * canvas.Width + x];
                    canvas.Pixels[y * canvas.Width + x] = new Rgba(c.r, c.g, c.b, c.a);
                }
            }

            UnityEngine.Object.DestroyImmediate(texture);
            return canvas;
        }

        /// <summary>Зберегти PNG через Texture2D.EncodeToPNG.</summary>
        public void Save(string path)
        {
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            Color32[] dst = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Rgba c = Pixels[y * Width + x];
                    dst[(Height - 1 - y) * Width + x] = new Color32(c.R, c.G, c.B, c.A);
                }
            }

            texture.SetPixels32(dst);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Rgba Get(int x, int y) => Inside(x, y) ? Pixels[y * Width + x] : Rgba.Clear;

        public void Set(int x, int y, Rgba c)
        {
            if (Inside(x, y))
            {
                Pixels[y * Width + x] = c;
            }
        }

        /// <summary>Накласти колір з його альфою поверх (звичайне змішування).</summary>
        public void Blend(int x, int y, Rgba c)
        {
            if (!Inside(x, y) || c.A == 0)
            {
                return;
            }

            if (c.A == 255)
            {
                Pixels[y * Width + x] = c;
                return;
            }

            Rgba d = Pixels[y * Width + x];
            float a = c.A / 255f;
            float da = d.A / 255f;
            float outA = a + da * (1f - a);
            if (outA <= 0f)
            {
                return;
            }

            byte Mix(byte s, byte t) => (byte)Math.Clamp((s * a + t * da * (1f - a)) / outA + 0.5f, 0f, 255f);
            Pixels[y * Width + x] = new Rgba(Mix(c.R, d.R), Mix(c.G, d.G), Mix(c.B, d.B), (byte)(outA * 255f + 0.5f));
        }

        public void Fill(Rgba c)
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = c;
            }
        }

        public void FillRect(int x, int y, int w, int h, Rgba c)
        {
            for (int j = y; j < y + h; j++)
            {
                for (int i = x; i < x + w; i++)
                {
                    Blend(i, j, c);
                }
            }
        }

        public void Rect(int x, int y, int w, int h, Rgba c)
        {
            HLine(x, x + w - 1, y, c);
            HLine(x, x + w - 1, y + h - 1, c);
            VLine(x, y, y + h - 1, c);
            VLine(x + w - 1, y, y + h - 1, c);
        }

        public void HLine(int x0, int x1, int y, Rgba c)
        {
            for (int x = Math.Min(x0, x1); x <= Math.Max(x0, x1); x++)
            {
                Blend(x, y, c);
            }
        }

        public void VLine(int x, int y0, int y1, Rgba c)
        {
            for (int y = Math.Min(y0, y1); y <= Math.Max(y0, y1); y++)
            {
                Blend(x, y, c);
            }
        }

        /// <summary>Лінія Брезенгема — рівні піксельні сходинки.</summary>
        public void Line(int x0, int y0, int x1, int y1, Rgba c)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Blend(x0, y0, c);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        /// <summary>Заповнене коло з піксельним краєм (центр у пікселі cx,cy).</summary>
        public void Disc(int cx, int cy, float r, Rgba c)
        {
            int ir = (int)Math.Ceiling(r);
            for (int y = -ir; y <= ir; y++)
            {
                for (int x = -ir; x <= ir; x++)
                {
                    if (x * x + y * y <= r * r + 0.25f)
                    {
                        Blend(cx + x, cy + y, c);
                    }
                }
            }
        }

        public void Ring(int cx, int cy, float r, Rgba c)
        {
            int ir = (int)Math.Ceiling(r) + 1;
            for (int y = -ir; y <= ir; y++)
            {
                for (int x = -ir; x <= ir; x++)
                {
                    float d = MathF.Sqrt(x * x + y * y);
                    if (MathF.Abs(d - r) < 0.5f)
                    {
                        Blend(cx + x, cy + y, c);
                    }
                }
            }
        }

        public void Blit(PixelCanvas src, int dx, int dy)
        {
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    Blend(dx + x, dy + y, src.Pixels[y * src.Width + x]);
                }
            }
        }

        /// <summary>Скопіювати прямокутник src у нове полотно.</summary>
        public PixelCanvas Crop(int x, int y, int w, int h)
        {
            PixelCanvas result = new PixelCanvas(w, h);
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    result.Pixels[j * w + i] = Get(x + i, y + j);
                }
            }

            return result;
        }

        /// <summary>Контур 1 px навколо непрозорого (для читабельності силуетів).</summary>
        public void Outline(Rgba c, bool diagonal = false)
        {
            Rgba[] copy = (Rgba[])Pixels.Clone();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (copy[y * Width + x].A > 0)
                    {
                        continue;
                    }

                    bool near = Opaque(copy, x - 1, y) || Opaque(copy, x + 1, y) || Opaque(copy, x, y - 1) || Opaque(copy, x, y + 1);
                    if (!near && diagonal)
                    {
                        near = Opaque(copy, x - 1, y - 1) || Opaque(copy, x + 1, y - 1) || Opaque(copy, x - 1, y + 1) || Opaque(copy, x + 1, y + 1);
                    }

                    if (near)
                    {
                        Pixels[y * Width + x] = c;
                    }
                }
            }
        }

        private bool Opaque(Rgba[] px, int x, int y) => Inside(x, y) && px[y * Width + x].A > 128;

        public PixelCanvas FlipH()
        {
            PixelCanvas result = new PixelCanvas(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    result.Pixels[y * Width + x] = Pixels[y * Width + (Width - 1 - x)];
                }
            }

            return result;
        }

        /// <summary>Поворот на 90° за годинниковою (для 4 напрямків у виді згори).</summary>
        public PixelCanvas Rotate90()
        {
            PixelCanvas result = new PixelCanvas(Height, Width);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    result.Pixels[x * Height + (Height - 1 - y)] = Pixels[y * Width + x];
                }
            }

            return result;
        }

        /// <summary>Детермінований шум 0..1 за координатою й зерном — однаковий арт при кожній генерації.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
