using System;
using System.Collections.Generic;

namespace Banda.ArtGen
{
    /// <summary>
    /// Екран Зломщика — масивні дверцята сейфа на весь екран: сталь з полосами прокату, заклепки, подряпини,
    /// петлі, штурвал ручки, латунна табличка виробника, три лампи кроків комбінації.
    /// Диск — окрема стрічка кадрів (по кадру на кожну з 40 позицій): насічки й цифри лишаються рівними пікселями
    /// при будь-якому повороті, без розмиття від обертання спрайта.
    /// </summary>
    public static class SafeArt
    {
        public const int DialSize = 112;
        public const int DialCenterX = 250;
        public const int DialCenterY = 186;

        public static Dictionary<string, PixelCanvas> All()
        {
            return new Dictionary<string, PixelCanvas>
            {
                { "safe_front", Front() },
                { "safe_dial_sheet", DialSheet(40) },
                { "safe_lamp_off", Lamp(P.Night3, P.Night4) },
                { "safe_lamp_on", Lamp(P.Ok, P.Lamp4) },
                { "safe_lamp_red", Lamp(P.Alarm, P.Neon3) },
                { "safe_marker", Marker() },
                { "safe_lcd", Lcd() },
            };
        }

        private static PixelCanvas Front()
        {
            PixelCanvas f = new PixelCanvas(640, 360);
            // Стіна кабінету за сейфом (видно по краях): шпалери в смужку, темні.
            for (int y = 0; y < 360; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    f.Set(x, y, (x / 6) % 2 == 0 ? P.Night2 : P.Night1);
                }
            }

            // Корпус сейфа.
            int bx = 40;
            int by = 10;
            int bw = 560;
            int bh = 350;
            f.FillRect(bx, by, bw, bh, P.Steel0);
            f.Rect(bx, by, bw, bh, P.Night0);
            // Дверцята: сталь з горизонтальними смугами прокату, світло зверху-ліворуч.
            int dx = bx + 26;
            int dy = by + 22;
            int dw = bw - 52;
            int dh = bh - 22;
            for (int y = dy; y < dy + dh; y++)
            {
                for (int x = dx; x < dx + dw; x++)
                {
                    float band = MathF.Sin(y * 0.11f + PixelCanvas.Hash(0, y / 3, 3) * 0.8f);
                    float light = 1f - (x - dx) / (float)dw * 0.5f - (y - dy) / (float)dh * 0.4f;
                    float v = light + band * 0.08f + (PixelCanvas.Hash(x, y, 13) - 0.5f) * 0.12f;
                    Rgba c = v > 0.85f ? P.Steel2 : v > 0.55f ? P.Steel1 : v > 0.3f ? P.Steel0 : P.Night4;
                    f.Set(x, y, c);
                }
            }

            f.Rect(dx, dy, dw, dh, P.Night0);
            f.HLine(dx + 1, dx + dw - 2, dy + 1, P.Steel3);
            f.VLine(dx + 1, dy + 1, dy + dh - 2, P.Steel2);
            f.Rect(dx + 10, dy + 10, dw - 20, dh - 20, P.Steel0); // фаска
            // Подряпини — короткі світлі діагоналі.
            for (int i = 0; i < 40; i++)
            {
                int sx = dx + 20 + (int)(PixelCanvas.Hash(i, 1, 77) * (dw - 40));
                int sy = dy + 20 + (int)(PixelCanvas.Hash(i, 2, 77) * (dh - 40));
                int len = 3 + (int)(PixelCanvas.Hash(i, 3, 77) * 9);
                f.Line(sx, sy, sx + len, sy + len / 3, P.Steel2.WithAlpha(150));
            }

            // Заклепки по периметру дверцят: світлий блік і тінь.
            for (int x = dx + 18; x < dx + dw - 10; x += 24)
            {
                Rivet(f, x, dy + 18);
                Rivet(f, x, dy + dh - 18);
            }

            for (int y = dy + 42; y < dy + dh - 30; y += 24)
            {
                Rivet(f, dx + 18, y);
                Rivet(f, dx + dw - 18, y);
            }

            // Петлі ліворуч.
            foreach (int hy in new[] { dy + 50, dy + dh - 90 })
            {
                f.FillRect(bx + 6, hy, 26, 40, P.Steel1);
                f.Rect(bx + 6, hy, 26, 40, P.Night0);
                f.HLine(bx + 7, bx + 30, hy + 1, P.Steel3);
                f.VLine(bx + 18, hy + 2, hy + 38, P.Steel0);
            }

            // Заглиблення під диск.
            f.Disc(DialCenterX, DialCenterY, DialSize / 2 + 10, P.Night3);
            f.Ring(DialCenterX, DialCenterY, DialSize / 2 + 10, P.Night0);
            f.Ring(DialCenterX, DialCenterY, DialSize / 2 + 9, P.Steel0);
            f.Ring(DialCenterX, DialCenterY, DialSize / 2 + 11, P.Steel2);

            // Штурвал ручки праворуч: три спиці з кулями.
            int hx = 450;
            int hy2 = 186;
            f.Disc(hx, hy2, 16, P.Steel1);
            f.Ring(hx, hy2, 16, P.Night0);
            f.Disc(hx, hy2, 8, P.Steel2);
            for (int k = 0; k < 3; k++)
            {
                float a = k * MathF.PI * 2f / 3f - MathF.PI / 2f;
                int ex = hx + (int)(MathF.Cos(a) * 48f);
                int ey = hy2 + (int)(MathF.Sin(a) * 48f);
                for (int t = -1; t <= 1; t++)
                {
                    f.Line(hx + t, hy2, ex + t, ey, P.Steel2);
                }

                f.Line(hx, hy2 + 2, ex, ey + 2, P.Night0);
                f.Disc(ex, ey, 7, P.Steel2);
                f.Ring(ex, ey, 7, P.Night0);
                f.Set(ex - 2, ey - 3, P.Steel3);
                f.Set(ex - 3, ey - 2, P.Steel3);
            }

            // Латунна табличка виробника.
            f.FillRect(250 - 70, 52, 140, 26, P.Lamp1);
            f.Rect(250 - 70, 52, 140, 26, P.Lamp0);
            f.HLine(250 - 69, 250 + 68, 53, P.Lamp3);
            PixelFont.DrawCentered(f, "ЗАЛІЗНИЙ ЛЕВ", 250, 56, P.Lamp0);
            PixelFont.DrawCentered(f, "1962", 250, 66, P.Lamp0);
            Rivet(f, 250 - 64, 65);
            Rivet(f, 250 + 64, 65);

            // Гнізда трьох ламп кроків комбінації над диском.
            for (int i = 0; i < 3; i++)
            {
                int lx = 250 - 30 + i * 30;
                f.Disc(lx, 100, 8, P.Night0);
                f.Ring(lx, 100, 8, P.Steel2);
            }

            // Віконце LCD під диском.
            f.FillRect(250 - 26, 270, 52, 18, P.Night0);
            f.Rect(250 - 26, 270, 52, 18, P.Steel2);
            return f;
        }

        private static void Rivet(PixelCanvas f, int x, int y)
        {
            f.Disc(x, y, 3, P.Steel1);
            f.Set(x - 1, y - 1, P.Steel3);
            f.Set(x + 1, y + 2, P.Night0);
            f.Set(x + 2, y + 1, P.Night0);
        }

        /// <summary>Стрічка кадрів диска: кадр k — диск повернутий так, що проти мітки вгорі цифра k.</summary>
        private static PixelCanvas DialSheet(int range)
        {
            PixelCanvas sheet = new PixelCanvas(DialSize * range, DialSize);
            for (int k = 0; k < range; k++)
            {
                sheet.Blit(Dial(k, range), k * DialSize, 0);
            }

            return sheet;
        }

        private static PixelCanvas Dial(int topNumber, int range)
        {
            int s = DialSize;
            int c = s / 2;
            PixelCanvas d = new PixelCanvas(s, s);
            float r = c - 2;
            // Тіло диска: чорнений метал з концентричним токарним слідом.
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - c + 0.5f;
                    float dy = y - c + 0.5f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r)
                    {
                        continue;
                    }

                    float ring = (int)dist % 3 == 0 ? 0.08f : 0f;
                    float light = 0.5f - (dx + dy) / (r * 4f) + ring;
                    Rgba col = light > 0.62f ? P.Steel1 : light > 0.42f ? P.Steel0 : P.Night4;
                    d.Set(x, y, col);
                }
            }

            d.Ring(c, c, r, P.Night0);
            d.Ring(c, c, r - 1, P.Steel2);
            // Насічки по краю: кожна поділка, довша — кожна п'ята. Цифри — кожна п'ята, завжди прямо.
            float step = MathF.PI * 2f / range;
            for (int i = 0; i < range; i++)
            {
                // Цифра i стоїть під кутом (i - topNumber) від верху (диск крутиться — цифри біжать по колу).
                float a = (i - topNumber) * step - MathF.PI / 2f;
                bool major = i % 5 == 0;
                float r0 = r - (major ? 9 : 5);
                d.Line(c + (int)MathF.Round(MathF.Cos(a) * r0), c + (int)MathF.Round(MathF.Sin(a) * r0),
                    c + (int)MathF.Round(MathF.Cos(a) * (r - 2)), c + (int)MathF.Round(MathF.Sin(a) * (r - 2)),
                    major ? P.Steel3 : P.Steel2);
                if (major)
                {
                    string label = i.ToString();
                    int tx = c + (int)MathF.Round(MathF.Cos(a) * (r - 18));
                    int ty = c + (int)MathF.Round(MathF.Sin(a) * (r - 18));
                    PixelFont.DrawCentered(d, label, tx, ty - 3, i == topNumber ? P.Lamp4 : P.Steel3);
                }
            }

            // Ручка-кноб у центрі.
            d.Disc(c, c, 16, P.Steel1);
            d.Ring(c, c, 16, P.Night0);
            d.Disc(c - 3, c - 3, 6, P.Steel2);
            d.Disc(c, c, 4, P.Steel0);
            // Риска на кнобі вказує на поточну цифру — допомагає бачити поворот.
            d.Line(c, c - 6, c, c - 14, P.Lamp3);
            return d;
        }

        private static PixelCanvas Lamp(Rgba body, Rgba spark)
        {
            PixelCanvas l = new PixelCanvas(14, 14);
            l.Disc(7, 7, 6, body);
            l.Ring(7, 7, 6, P.Night0);
            l.Set(5, 4, spark);
            l.Set(4, 5, spark);
            return l;
        }

        private static PixelCanvas Marker()
        {
            PixelCanvas m = new PixelCanvas(11, 8);
            for (int y = 0; y < 6; y++)
            {
                m.HLine(y, 10 - y, y, P.Lamp3);
            }

            m.HLine(0, 10, 0, P.Lamp4);
            return m;
        }

        private static PixelCanvas Lcd()
        {
            PixelCanvas l = new PixelCanvas(50, 16);
            l.Fill(P.Cyan0);
            for (int x = 0; x < 50; x += 2)
            {
                l.Set(x, 0, P.Cyan1);
            }

            return l;
        }
    }
}
