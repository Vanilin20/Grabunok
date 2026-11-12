using System;
using System.Collections.Generic;
using System.IO;

namespace Banda.ArtGen
{
    /// <summary>
    /// Підсумок справи — газетна шпальта (ТЗ: «газетна шпальта про банду»). Сторінка 360×300 на темному столі:
    /// газетний папір з плямами від кави, шапка видання, колонки «тексту», фото банди растром (з арту меню).
    /// Заголовок і цифри справи — живий текст TMP поверх (залежать від результату).
    /// </summary>
    public static class NewspaperArt
    {
        public const int PageW = 360;
        public const int PageH = 300;
        /// <summary>Прямокутник під живий заголовок (TMP) — на сторінці лишається порожнім.</summary>
        public const int HeadlineY = 50;
        public const int HeadlineH = 40;

        public static Dictionary<string, PixelCanvas> All(string projectRoot)
        {
            return new Dictionary<string, PixelCanvas>
            {
                { "news_page", Page(projectRoot) },
                { "news_stamp_fail", Stamp("ПРОВАЛ", P.Alarm) },
                { "news_stamp_win", Stamp("СЕНСАЦІЯ", P.Neon1) },
                { "news_table", Table() },
            };
        }

        private static PixelCanvas Table()
        {
            PixelCanvas t = new PixelCanvas(640, 360);
            for (int y = 0; y < 360; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    float n = PixelCanvas.Hash(x / 5, y, 303);
                    t.Set(x, y, n < 0.3f ? P.Brick0 : P.Night2);
                }
            }

            // Тепла пляма настільної лампи над газетою.
            for (int y = 0; y < 360; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    float d = MathF.Sqrt((x - 320) * (x - 320) * 0.6f + (y - 160) * (y - 160)) / 260f;
                    if (d < 1f && PixelCanvas.Hash(x, y, 9) < (1f - d) * 0.7f)
                    {
                        t.Set(x, y, d < 0.5f ? P.Brick1 : P.Lamp0);
                    }
                }
            }

            return t;
        }

        private static PixelCanvas Page(string projectRoot)
        {
            PixelCanvas p = new PixelCanvas(PageW, PageH);
            // Газетний папір: теплий світлий з волокнами, краї потемнілі.
            for (int y = 0; y < PageH; y++)
            {
                for (int x = 0; x < PageW; x++)
                {
                    float n = PixelCanvas.Hash(x, y, 404);
                    float edge = MathF.Min(MathF.Min(x, PageW - 1 - x), MathF.Min(y, PageH - 1 - y));
                    Rgba c = n < 0.08f ? P.Steel2 : P.Steel3;
                    if (edge < 3 || (edge < 6 && n < 0.4f))
                    {
                        c = P.Steel2;
                    }

                    p.Set(x, y, c);
                }
            }

            // Пляма від кави.
            p.Ring(300, 240, 18, P.Lamp1.WithAlpha(120));
            p.Ring(300, 240, 17, P.Lamp2.WithAlpha(60));

            // Шапка видання.
            PixelFont.DrawCentered(p, "НІЧНЕ МІСТО", PageW / 2, 10, P.Night1, 3);
            p.HLine(10, PageW - 11, 36, P.Night1);
            p.HLine(10, PageW - 11, 38, P.Night1);
            PixelFont.Draw(p, "№ 1", 12, 41, P.Night4);
            PixelFont.Draw(p, "ЦІНА 5 ₴", PageW - 12 - PixelFont.Measure("ЦІНА 5 ₴"), 41, P.Night4);
            p.HLine(10, PageW - 11, HeadlineY + HeadlineH + 2, P.Night1);

            // Фото банди растром: силуети з арту меню.
            int photoX = 14;
            int photoY = HeadlineY + HeadlineH + 10;
            int photoW = 170;
            int photoH = 120;
            DrawPhoto(p, projectRoot, photoX, photoY, photoW, photoH);
            PixelFont.Draw(p, "ФОТО: ЗАЛ. АРХІВ", photoX, photoY + photoH + 4, P.Night4);

            // Колонки «тексту» праворуч і внизу — сірі рядки різної довжини.
            int colX = photoX + photoW + 12;
            for (int y = photoY; y < PageH - 16; y += 5)
            {
                int len = PageW - 14 - colX - (int)(PixelCanvas.Hash(y, 1, 505) * 30);
                p.HLine(colX, colX + len, y, P.Night5);
                p.HLine(colX, colX + len - 4, y + 1, P.Steel1);
            }

            for (int y = photoY + photoH + 14; y < PageH - 12; y += 5)
            {
                int len = photoW - (int)(PixelCanvas.Hash(y, 2, 505) * 25);
                p.HLine(photoX, photoX + len, y, P.Night5);
            }

            // Згин газети посередині.
            p.VLine(PageW / 2, 40, PageH - 2, P.Steel2);
            return p;
        }

        private static void DrawPhoto(PixelCanvas p, string projectRoot, int x0, int y0, int w, int h)
        {
            p.FillRect(x0, y0, w, h, P.Steel2);
            p.Rect(x0, y0, w, h, P.Night1);
            string menu = Path.Combine(projectRoot, "Assets", "Art", "Menu");
            (string file, int fw, int fh, int px)[] gang =
            {
                ("gang_runner.png", 48, 76, 20), ("gang_leader.png", 72, 114, 58), ("gang_hacker.png", 67, 106, 110),
            };
            foreach ((string file, int fw, int fh, int px) in gang)
            {
                string path = Path.Combine(menu, file);
                if (!File.Exists(path))
                {
                    continue;
                }

                PixelCanvas sheet = PixelCanvas.Load(path);
                PixelCanvas frame = sheet.Crop(0, 0, fw, fh);
                // Растр: світлі ділянки — дрібні точки, темні — великі. Силует банди читається навіть на папері.
                int ox = x0 + px;
                int oy = y0 + h - fh;
                for (int y = 0; y < fh; y++)
                {
                    for (int x = 0; x < fw; x++)
                    {
                        Rgba c = frame.Get(x, y);
                        if (c.A < 100 || oy + y < y0 + 1)
                        {
                            continue;
                        }

                        float l = (c.R + c.G + c.B) / 765f;
                        bool dot = (x + y) % 2 == 0 ? l < 0.55f : l < 0.25f;
                        p.Set(ox + x, oy + y, dot ? P.Night1 : P.Steel1);
                    }
                }
            }

            // Неонова вивіска на фото — як розмита сіра пляма вгорі.
            for (int x = x0 + 30; x < x0 + w - 30; x += 2)
            {
                p.Set(x, y0 + 8, P.Night4);
                p.Set(x + 1, y0 + 10, P.Night4);
            }
        }

        private static PixelCanvas Stamp(string text, Rgba color)
        {
            int w = PixelFont.Measure(text) * 2 + 16;
            PixelCanvas s = new PixelCanvas(w, 26);
            s.Rect(0, 0, w, 26, color);
            s.Rect(2, 2, w - 4, 22, color);
            PixelFont.DrawCentered(s, text, w / 2, 6, color, 2);
            // Нерівний відбиток штампа — пропуски фарби.
            for (int y = 0; y < 26; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (PixelCanvas.Hash(x, y, 606) < 0.18f)
                    {
                        s.Set(x, y, Rgba.Clear);
                    }
                }
            }

            return s;
        }
    }
}
