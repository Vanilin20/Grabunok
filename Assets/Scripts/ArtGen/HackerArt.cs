using System;
using System.Collections.Generic;
using Banda.Heist;

namespace Banda.ArtGen
{
    /// <summary>
    /// Екран Хакера — ноутбук на весь екран (діегетичний інтерфейс, стандарт R9):
    /// корпус з наліпками на темному столі, CRT-сканлайни й віньєтка поверх, схема магазину як на моніторі,
    /// стрічка з камери з шумом. Інтерактив (камери, двері, охоронець) — UI-іконки поверх схеми.
    /// </summary>
    public static class HackerArt
    {
        public const int ScreenX = 64;
        public const int ScreenY = 18;
        public const int ScreenW = 512;
        public const int ScreenH = 280;
        /// <summary>Де на екрані ноутбука лежить схема і в якому масштабі (1 px рівня = SchemeScale px).</summary>
        public const int SchemeX = 20;
        public const int SchemeY = 40;

        public static Dictionary<string, PixelCanvas> All()
        {
            HeistLevel level = HeistLevel.CornerStore();
            Dictionary<string, PixelCanvas> d = new Dictionary<string, PixelCanvas>
            {
                { "laptop_frame", LaptopFrame() },
                { "crt_scan", Scanline() },
                { "crt_vignette", Vignette(ScreenW, ScreenH) },
                { "hacker_schematic", Schematic(level) },
                { "h_cam_on", CamIcon(false) },
                { "h_cam_off", CamIcon(true) },
                { "h_door_closed", DoorIcon(false) },
                { "h_door_open", DoorIcon(true) },
                { "h_guard", GuardIcon() },
                { "h_feed_frame", FeedFrame() },
            };
            PixelCanvas feed = CameraFeed(level);
            d.Add("h_feed", feed);
            for (int i = 0; i < 4; i++)
            {
                d.Add("crt_noise_" + i, Noise(feed.Width, feed.Height, i));
            }

            return d;
        }

        /// <summary>Корпус ноутбука 640×360 з прозорим вікном екрана.</summary>
        private static PixelCanvas LaptopFrame()
        {
            PixelCanvas f = new PixelCanvas(640, 360);
            // Стіл: темне дерево з волокнами, теплий відсвіт від настільної лампи зліва.
            for (int y = 0; y < 360; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    float n = PixelCanvas.Hash(x / 4, y, 101);
                    Rgba c = n < 0.35f ? P.Brick0 : P.Night2;
                    if ((y + (int)(MathF.Sin(x * 0.03f) * 3f)) % 9 == 0)
                    {
                        c = P.Night1;
                    }

                    f.Set(x, y, c);
                }
            }

            for (int y = 0; y < 360; y++)
            {
                for (int x = 0; x < 140; x++)
                {
                    float k = 1f - MathF.Sqrt((x - 10) * (x - 10) + (y - 300) * (y - 300)) / 170f;
                    if (k > 0f && PixelCanvas.Hash(x, y, 7) < k * 0.6f)
                    {
                        f.Set(x, y, P.Lamp0); // тепла пляма від лампи
                    }
                }
            }

            // Кришка з екраном.
            int lx = ScreenX - 14;
            int ly = ScreenY - 12;
            int lw = ScreenW + 28;
            int lh = ScreenH + 26;
            f.FillRect(lx, ly, lw, lh, P.Night2);
            f.Rect(lx, ly, lw, lh, P.Night0);
            f.HLine(lx + 1, lx + lw - 2, ly + 1, P.Night4);
            f.VLine(lx + 1, ly + 1, ly + lh - 2, P.Night3);
            // Вебкамера, заклеєна ізолентою — хакер же.
            f.FillRect(320 - 6, ly + 3, 12, 5, P.Night0);
            f.FillRect(320 - 5, ly + 4, 10, 3, P.Lamp1);
            // Наліпки на рамці.
            f.FillRect(lx + 6, ly + lh - 18, 14, 10, P.Neon1);
            PixelFont.Draw(f, "B", lx + 9, ly + lh - 17, P.Neon3);
            f.FillRect(lx + lw - 24, ly + lh - 16, 16, 8, P.Cyan1);
            f.HLine(lx + lw - 22, lx + lw - 11, ly + lh - 12, P.Cyan3);
            // Жовтий папірець з «паролем».
            f.FillRect(lx + lw - 70, ly + lh - 10, 40, 14, P.Lamp3);
            f.HLine(lx + lw - 66, lx + lw - 40, ly + lh - 6, P.Lamp1);
            f.HLine(lx + lw - 66, lx + lw - 50, ly + lh - 2, P.Lamp1);

            // Екран — прозорий (під ним UI зі схемою).
            for (int y = ScreenY; y < ScreenY + ScreenH; y++)
            {
                for (int x = ScreenX; x < ScreenX + ScreenW; x++)
                {
                    f.Set(x, y, Rgba.Clear);
                }
            }

            f.Rect(ScreenX - 1, ScreenY - 1, ScreenW + 2, ScreenH + 2, P.Night0);

            // Петля й клавіатура: верхній ряд клавіш з блакитним підсвічуванням.
            int ky = ly + lh;
            f.FillRect(lx - 30, ky, lw + 60, 360 - ky, P.Night3);
            f.HLine(lx - 30, lx + lw + 29, ky, P.Night5);
            f.FillRect(lx + 30, ky - 2, lw - 60, 3, P.Night1); // петля
            for (int row = 0; row < 3; row++)
            {
                for (int k = 0; k < 18; k++)
                {
                    int kx = lx + 10 + k * 30 + (row % 2) * 8;
                    int kyy = ky + 8 + row * 16;
                    if (kyy + 12 > 360)
                    {
                        continue;
                    }

                    f.FillRect(kx, kyy, 26, 12, P.Night1);
                    f.HLine(kx, kx + 25, kyy + 12, P.Cyan0); // підсвічування з-під клавіш
                    f.HLine(kx + 1, kx + 24, kyy, P.Night4);
                }
            }

            // Чашка кави на столі зліва (видно край і пару).
            f.Disc(26, 250, 14, P.Steel2);
            f.Disc(26, 250, 11, P.Brick0);
            f.Disc(26, 250, 10, P.Lamp0);
            f.Ring(26, 250, 14, P.Steel1);
            f.FillRect(40, 246, 6, 8, P.Steel2);
            return f;
        }

        private static PixelCanvas Scanline()
        {
            PixelCanvas s = new PixelCanvas(1, 2);
            s.Set(0, 1, P.Night0.WithAlpha(70));
            return s;
        }

        /// <summary>Затемнення кутів кінескопа — сходинками (без згладжування), щоб лишалось піксельним.</summary>
        private static PixelCanvas Vignette(int w, int h)
        {
            PixelCanvas v = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - w / 2f) / (w / 2f);
                    float dy = (y - h / 2f) / (h / 2f);
                    float d = MathF.Pow(MathF.Pow(MathF.Abs(dx), 4f) + MathF.Pow(MathF.Abs(dy), 4f), 0.25f);
                    float a = MathF.Max(0f, (d - 0.78f) / 0.22f);
                    a = MathF.Floor(a * 5f) / 5f; // 5 сходинок
                    v.Set(x, y, P.Night0.WithAlpha((byte)(a * 200f)));
                }
            }

            return v;
        }

        /// <summary>Схема магазину «як на моніторі»: креслення ліній, підписи зон.</summary>
        public static PixelCanvas Schematic(HeistLevel level)
        {
            int w = (int)level.Size.X;
            int h = (int)level.Size.Y;
            PixelCanvas s = new PixelCanvas(w + 2, h + 2);
            // Сітка креслення.
            for (int y = 0; y < s.Height; y++)
            {
                for (int x = 0; x < s.Width; x++)
                {
                    if (x % 16 == 0 || y % 16 == 0)
                    {
                        s.Set(x, y, P.Cyan0.WithAlpha(160));
                    }
                }
            }

            foreach (Box wall in level.Walls)
            {
                s.FillRect((int)wall.X + 1, (int)wall.Y + 1, (int)wall.W, (int)wall.H, P.Cyan1.WithAlpha(120));
                s.Rect((int)wall.X + 1, (int)wall.Y + 1, (int)wall.W, (int)wall.H, P.Cyan2);
            }

            foreach (Box cover in level.Covers)
            {
                int cx = (int)cover.X + 1;
                int cy = (int)cover.Y + 1;
                s.Rect(cx, cy, (int)cover.W, (int)cover.H, P.Cyan2.WithAlpha(200));
                for (int x = cx + 2; x < cx + cover.W - 2; x += 4)
                {
                    s.Line(x, cy + (int)cover.H - 2, x + 2, cy + 1, P.Cyan1); // штрихування
                }
            }

            foreach (Box exit in level.Exits)
            {
                s.Rect((int)exit.X + 1, (int)exit.Y + 1, (int)exit.W, (int)exit.H, P.Ok);
            }

            s.Ring((int)level.SafePos.X + 1, (int)level.SafePos.Y + 1, 6, P.Lamp3);
            s.Disc((int)level.SafePos.X + 1, (int)level.SafePos.Y + 1, 2, P.Lamp3);
            PixelFont.Draw(s, "ЗАЛ", 6 * 16, 11 * 16 + 2, P.Cyan1);
            PixelFont.Draw(s, "КАСА", 12 * 16 + 6, 10 * 16 + 4, P.Cyan1);
            PixelFont.Draw(s, "ПІДСОБКА", 19 * 16, 12 * 16 + 4, P.Cyan1);
            PixelFont.Draw(s, "СЕЙФ", 22 * 16, 2 * 16 + 4, P.Lamp3);
            PixelFont.Draw(s, "ВХІД", 2 * 16 + 4, 12 * 16 + 4, P.Ok);
            return s;
        }

        private static PixelCanvas CamIcon(bool off)
        {
            PixelCanvas c = new PixelCanvas(14, 12);
            Rgba col = off ? P.Alarm : P.Ok;
            c.FillRect(1, 3, 9, 6, col);
            c.FillRect(10, 4, 3, 4, col);
            c.Disc(5, 6, 1.5f, P.Night0);
            if (off)
            {
                c.Line(0, 11, 13, 0, P.Lamp4);
            }

            return c;
        }

        private static PixelCanvas DoorIcon(bool open)
        {
            PixelCanvas d = new PixelCanvas(12, 16);
            d.Rect(0, 0, 12, 16, P.Cyan2);
            if (open)
            {
                d.Line(1, 15, 9, 9, P.Ok);
                d.Line(1, 14, 9, 8, P.Ok);
            }
            else
            {
                d.FillRect(2, 2, 8, 12, P.Alarm);
                d.Set(8, 8, P.Lamp4);
            }

            return d;
        }

        private static PixelCanvas GuardIcon()
        {
            PixelCanvas g = new PixelCanvas(9, 9);
            g.Disc(4, 4, 3.5f, P.Alarm);
            g.Disc(4, 4, 1.5f, P.Neon3);
            return g;
        }

        private static PixelCanvas FeedFrame()
        {
            PixelCanvas f = new PixelCanvas(108, 80);
            f.Rect(0, 0, 108, 80, P.Cyan2);
            f.FillRect(0, 0, 108, 9, P.Cyan0);
            PixelFont.Draw(f, "CAM 1", 3, 1, P.Cyan3);
            f.Disc(100, 4, 2, P.Alarm);
            return f;
        }

        /// <summary>Знімок з камери: освітлений рівень біля камери, перефарбований у моніторний зелено-блакитний.</summary>
        private static PixelCanvas CameraFeed(HeistLevel level)
        {
            // Камера бачить зал у своєму ІЧ-режимі: без кольору світла, лише яскравість поверхонь — збираємо з тайлів.
            PixelCanvas lit = TileArt.Compose(HeistLevel.StoreGrid);
            Vec2 cam = level.Cameras[0];
            int cx = (int)cam.X;
            int cy = (int)cam.Y + 16; // камера дивиться вниз, у зал
            PixelCanvas crop = lit.Crop(cx - 50, cy - 28, 104, 70);
            PixelCanvas feed = new PixelCanvas(104, 70);
            Rgba[] ramp = { P.Night0, P.Cyan0, P.Cyan1, P.Cyan2, P.Cyan3 };
            for (int i = 0; i < crop.Pixels.Length; i++)
            {
                Rgba c = crop.Pixels[i];
                float l = (c.R * 0.3f + c.G * 0.5f + c.B * 0.2f) / 255f;
                int k = Math.Clamp((int)(l * 2.4f * ramp.Length), 0, ramp.Length - 1);
                feed.Pixels[i] = ramp[k];
            }

            return feed;
        }

        private static PixelCanvas Noise(int w, int h, int seed)
        {
            PixelCanvas n = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = PixelCanvas.Hash(x, y, 200 + seed);
                    if (v > 0.92f)
                    {
                        n.Set(x, y, P.Cyan3.WithAlpha(90));
                    }
                    else if (v < 0.05f)
                    {
                        n.Set(x, y, P.Night0.WithAlpha(120));
                    }
                }
            }

            // Смуга перешкоди — у кожному кадрі на іншій висоті.
            int band = (seed * 17) % h;
            n.FillRect(0, band, w, 2, P.Cyan3.WithAlpha(40));
            return n;
        }
    }
}
