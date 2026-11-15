using System;
using UnityEngine;

namespace Banda.HeistRuntime
{
    /// <summary>Режим вікна головоломки на ноутбуці Хакера.</summary>
    public enum HackMode
    {
        Hidden,
        Puzzle,
        Solved,
        Locked,
    }

    /// <summary>Усе, що потрібно намалювати одного кадру. Малювальник не тримає стану гри.</summary>
    public sealed class HackBoardState
    {
        public HackMode Mode = HackMode.Puzzle;
        public int Wires = 4;
        /// <summary>Кольори правих гнізд зверху вниз.</summary>
        public int[] Right = Array.Empty<int>();
        /// <summary>Для кожного дроту — гніздо, куди він вставлений (-1 — ні).</summary>
        public int[] SocketOf = Array.Empty<int>();
        /// <summary>Дріт, який зараз тягнуть пальцем (-1 — жодного) і де палець.</summary>
        public int Dragging = -1;
        public Vector2 DragPos;
        /// <summary>Скільки часу лишилось, частка 0..1.</summary>
        public float TimeFrac = 1f;
        public bool CanSubmit;
        public bool Busy;
        public bool Pressed;
        /// <summary>Секунди з відкриття (анімація світіння, мерехтіння).</summary>
        public float Clock;
        /// <summary>Спалах результату 0..1 (зелений для успіху, червоний для провалу).</summary>
        public float Flash;
        public bool Failed;
        /// <summary>Скільки блокування лишилось, частка 0..1 (для кнопки-відліку).</summary>
        public float LockFrac;
        /// <summary>Іскра від щойно вставленого штекера: де і скільки ще триває (0..1).</summary>
        public Vector2 SparkPos;
        public float Spark;
        public int SparkColor;
    }

    /// <summary>
    /// Піксельний малювальник вікна «Взлом»: пише в масив Color32 розміру 354×210 (1 піксель = 1 піксель базової роздільності),
    /// який HackBoard заливає в текстуру Point. Дроти, штекери, гнізда, кнопка — піксель-арт у палітрі гри
    /// (3–4 тони на матеріал, контур, блік), а не векторні примітиви. Тексти малює TextMeshPro поверх.
    /// </summary>
    public sealed class HackBoardPainter
    {
        public const int W = 354;
        public const int H = 210;
        public const int SocketInset = 34;
        public const float PickRadius = 20f;
        public static readonly RectInt SubmitRect = new RectInt(107, 182, 140, 22);
        public static readonly RectInt CancelRect = new RectInt(335, 2, 14, 12);

        // --- Палітра (docs/art/palette.gpl) ---
        private static readonly Color32 Night0 = C(7, 6, 13);
        private static readonly Color32 Steel0 = C(58, 63, 79);
        private static readonly Color32 Steel1 = C(90, 97, 117);
        private static readonly Color32 Steel2 = C(139, 147, 168);
        private static readonly Color32 Steel3 = C(199, 206, 222);
        private static readonly Color32 Cyan0 = C(15, 58, 77);
        private static readonly Color32 Cyan1 = C(26, 127, 153);
        private static readonly Color32 Cyan2 = C(61, 224, 255);
        private static readonly Color32 Cyan3 = C(194, 246, 255);
        private static readonly Color32 Alarm = C(232, 67, 58);
        private static readonly Color32 Ok = C(92, 240, 138);
        private static readonly Color32 Board = C(5, 12, 22);
        private static readonly Color32 Grid = C(9, 26, 40);
        private static readonly Color32 Trace = C(12, 40, 58);

        /// <summary>Основний, світлий і темний тони кожного кольору дроту. 0 червоний, 1 жовтий, 2 зелений, 3 блакитний, 4 рожевий.</summary>
        private static readonly Color32[][] WireTones =
        {
            new[] { C(232, 67, 58), C(255, 150, 130), C(110, 25, 30) },
            new[] { C(247, 201, 90), C(255, 242, 196), C(138, 90, 36) },
            new[] { C(92, 240, 138), C(190, 255, 205), C(24, 100, 58) },
            new[] { C(61, 224, 255), C(194, 246, 255), C(15, 80, 110) },
            new[] { C(255, 61, 127), C(255, 179, 207), C(92, 16, 54) },
        };

        /// <summary>Позначки на штекерах — щоб кольори не були єдиною підказкою. 5×5, рядки зверху вниз.</summary>
        private static readonly string[][] Symbols =
        {
            new[] { ".###.", "#####", "#####", "#####", ".###." }, // коло
            new[] { "#####", "#####", "#####", "#####", "#####" }, // квадрат
            new[] { "..#..", ".###.", ".###.", "#####", "#####" }, // трикутник
            new[] { "..#..", ".###.", "#####", ".###.", "..#.." }, // ромб
            new[] { "..#..", "..#..", "#####", "..#..", "..#.." }, // хрест
        };

        private readonly Color32[] _background = new Color32[W * H];
        private readonly Color32[] _pixels = new Color32[W * H];
        private readonly System.Random _noise = new System.Random(4242);

        public HackBoardPainter()
        {
            BuildBackground();
        }

        public Color32[] Pixels => _pixels;

        // ------------------------------------------------------------------ розкладка

        public static float WireY(int slot, int count)
        {
            float top = 46f;
            float bottom = 160f;
            return count <= 1 ? (top + bottom) * 0.5f : Mathf.Round(top + (bottom - top) * slot / (count - 1));
        }

        public static Vector2 LeftStart(int wire, int count) => new Vector2(35f, WireY(wire, count));

        public static Vector2 RightEnd(int slot, int count) => new Vector2(W - SocketInset, WireY(slot, count));

        // ------------------------------------------------------------------ кадр

        public void Paint(HackBoardState s)
        {
            Array.Copy(_background, _pixels, _pixels.Length);
            if (s.Mode == HackMode.Hidden)
            {
                return;
            }

            DrawTimerBar(s);
            int n = s.Wires;
            // Гнізда й штекери під дротами, щоб дріт «виходив» із корпусу.
            for (int i = 0; i < n; i++)
            {
                DrawLeftPlug(i, n, s);
                DrawRightSocket(i, n, s);
            }

            for (int i = 0; i < n; i++)
            {
                if (s.SocketOf[i] >= 0 && i != s.Dragging)
                {
                    Vector2 a = LeftStart(i, n);
                    Vector2 b = RightEnd(s.SocketOf[i], n);
                    DrawWire(a, b, i, sag: 7f, s.Clock);
                }
            }

            if (s.Dragging >= 0)
            {
                Vector2 a = LeftStart(s.Dragging, n);
                Vector2 b = new Vector2(Mathf.Clamp(s.DragPos.x, 36f, W - 4f), Mathf.Clamp(s.DragPos.y, 22f, H - 30f));
                DrawWire(a, b, s.Dragging, sag: 12f + Mathf.Abs(b.x - a.x) * 0.04f, s.Clock);
                DrawTip(b, s.Dragging, s.Clock);
            }

            DrawSubmit(s);
            DrawSpark(s);
            DrawFlash(s);
            DrawCrtBar(s);
        }

        // ------------------------------------------------------------------ фон

        private void BuildBackground()
        {
            Fill(_background, Board);
            // Сітка плати.
            for (int x = 0; x < W; x += 12)
            {
                for (int y = 0; y < H; y++)
                {
                    Put(_background, x, y, Grid);
                }
            }

            for (int y = 0; y < H; y += 12)
            {
                for (int x = 0; x < W; x++)
                {
                    Put(_background, x, y, Grid);
                }
            }

            // Доріжки плати: ламані лінії з вузлами — щоб вікно не було ідеальною сіткою.
            System.Random rng = new System.Random(77);
            for (int k = 0; k < 14; k++)
            {
                int x = 40 + rng.Next(0, W - 80);
                int y = 24 + rng.Next(0, 140);
                int len = 18 + rng.Next(0, 50);
                bool horizontal = rng.Next(0, 2) == 0;
                for (int j = 0; j < len; j++)
                {
                    Put(_background, horizontal ? x + j : x, horizontal ? y : y + j, Trace);
                }

                int x2 = horizontal ? x + len : x;
                int y2 = horizontal ? y : y + len;
                int len2 = 6 + rng.Next(0, 22);
                for (int j = 0; j < len2; j++)
                {
                    Put(_background, horizontal ? x2 : x2 + j, horizontal ? y2 + j : y2, Trace);
                }

                FillRect(_background, x - 1, y - 1, 3, 3, Cyan0);
            }

            // Пил і мертві пікселі.
            for (int k = 0; k < 90; k++)
            {
                Put(_background, 36 + rng.Next(0, W - 72), 22 + rng.Next(0, 150), rng.Next(0, 3) == 0 ? Cyan0 : Grid);
            }

            // Шини з болтами по боках: звідси виходять дроти.
            for (int side = 0; side < 2; side++)
            {
                int x0 = side == 0 ? 1 : W - 8;
                FillRect(_background, x0, 20, 7, 154, Steel0);
                FillRect(_background, x0, 20, 7, 1, Steel2);
                FillRect(_background, side == 0 ? x0 : x0 + 6, 20, 1, 154, Steel1);
                FillRect(_background, side == 0 ? x0 + 6 : x0, 20, 1, 154, Night0);
                for (int y = 28; y < 170; y += 22)
                {
                    FillRect(_background, x0 + 2, y, 3, 3, Steel1);
                    Put(_background, x0 + 2, y, Steel3);
                    Put(_background, x0 + 4, y + 2, Night0);
                }
            }

            // Заголовок вікна.
            FillRect(_background, 1, 1, W - 2, 14, Cyan0);
            FillRect(_background, 1, 1, W - 2, 1, Cyan1);
            FillRect(_background, 1, 14, W - 2, 1, Cyan2);
            for (int x = 1; x < W - 1; x += 3)
            {
                Put(_background, x, 12, C(11, 46, 62));
            }

            // Кнопка закриття (Х).
            FillRect(_background, CancelRect.x, CancelRect.y, CancelRect.width, CancelRect.height, C(92, 24, 28));
            FillRect(_background, CancelRect.x, CancelRect.y, CancelRect.width, 1, C(176, 70, 66));
            FillRect(_background, CancelRect.x, CancelRect.yMax - 1, CancelRect.width, 1, C(40, 10, 14));
            for (int d = 0; d < 6; d++)
            {
                Put(_background, CancelRect.x + 4 + d, CancelRect.y + 3 + d, Alarm);
                Put(_background, CancelRect.x + 9 - d, CancelRect.y + 3 + d, Alarm);
            }

            // Смуга таймера: жолоб.
            FillRect(_background, 6, 18, W - 12, 5, Night0);
            FillRect(_background, 6, 18, W - 12, 1, C(2, 3, 8));
            FillRect(_background, 6, 22, W - 12, 1, Cyan0);

            // Підвал: розділювач і кутові скоби.
            FillRect(_background, 1, 176, W - 2, 1, Cyan0);
            FillRect(_background, 1, 177, W - 2, 1, Night0);
            FillRect(_background, 1, H - 3, W - 2, 2, Cyan0);
            for (int i = 0; i < 4; i++)
            {
                FillRect(_background, 4 + i * 4, 186, 2, 14, i % 2 == 0 ? Cyan0 : Board);
                FillRect(_background, W - 8 - i * 4, 186, 2, 14, i % 2 == 0 ? Cyan0 : Board);
            }

            // Рамка вікна з фаскою.
            Outline(_background, 0, 0, W, H, Cyan2);
            Outline(_background, 1, 1, W - 2, H - 2, Cyan1);
            Put(_background, 0, 0, Cyan3);
            Put(_background, W - 1, 0, Cyan3);
        }

        // ------------------------------------------------------------------ деталі

        private void DrawTimerBar(HackBoardState s)
        {
            float frac = Mathf.Clamp01(s.TimeFrac);
            int width = Mathf.RoundToInt((W - 14) * frac);
            if (width <= 0)
            {
                return;
            }

            bool urgent = frac < 0.3f;
            bool blink = urgent && ((int)(s.Clock * 6f) & 1) == 0;
            Color32 hi = frac > 0.5f ? Cyan3 : frac > 0.3f ? C(255, 242, 196) : C(255, 150, 130);
            Color32 mid = frac > 0.5f ? Cyan2 : frac > 0.3f ? C(247, 201, 90) : Alarm;
            Color32 lo = frac > 0.5f ? Cyan1 : frac > 0.3f ? C(138, 90, 36) : C(110, 25, 30);
            if (blink)
            {
                mid = hi;
            }

            FillRect(_pixels, 7, 19, width, 4, mid);
            FillRect(_pixels, 7, 19, width, 1, hi);
            FillRect(_pixels, 7, 22, width, 1, lo);
            Blend(_pixels, 7, 24, width, 3, mid, 0.18f);
            // Риски на шкалі — читається як вимірювальний прилад.
            for (int x = 7 + 18; x < 7 + width; x += 18)
            {
                Put(_pixels, x, 20, lo);
                Put(_pixels, x, 21, lo);
            }
        }

        private void DrawLeftPlug(int i, int n, HackBoardState s)
        {
            int y = (int)WireY(i, n);
            Color32[] t = WireTones[i % WireTones.Length];
            bool held = s.Dragging == i;
            // Корпус затискача.
            FillRect(_pixels, 9, y - 9, 20, 18, Steel0);
            FillRect(_pixels, 9, y - 9, 20, 2, Steel1);
            FillRect(_pixels, 9, y - 9, 20, 1, Steel2);
            FillRect(_pixels, 9, y + 7, 20, 2, Night0);
            FillRect(_pixels, 9, y - 9, 1, 18, Steel2);
            FillRect(_pixels, 28, y - 9, 1, 18, Night0);
            Put(_pixels, 11, y - 6, Steel3);
            Put(_pixels, 11, y + 5, Steel3);
            // Кольорова муфта.
            FillRect(_pixels, 26, y - 7, 8, 14, t[0]);
            FillRect(_pixels, 26, y - 7, 8, 3, t[1]);
            FillRect(_pixels, 26, y + 4, 8, 3, t[2]);
            FillRect(_pixels, 26, y - 7, 1, 14, t[2]);
            Outline(_pixels, 25, y - 8, 10, 16, Night0);
            Blend(_pixels, 24, y - 10, 14, 20, t[0], 0.10f);
            DrawSymbol(15, y - 3, i, held ? Steel3 : t[1], Night0);
            if (held)
            {
                Outline(_pixels, 7, y - 11, 28, 22, Cyan2);
            }
        }

        private void DrawRightSocket(int slot, int n, HackBoardState s)
        {
            int y = (int)WireY(slot, n);
            int colorIndex = slot < s.Right.Length ? s.Right[slot] : 0;
            Color32[] t = WireTones[colorIndex % WireTones.Length];
            int x = W - 29;
            FillRect(_pixels, x, y - 9, 20, 18, Steel0);
            FillRect(_pixels, x, y - 9, 20, 2, Steel1);
            FillRect(_pixels, x, y - 9, 20, 1, Steel2);
            FillRect(_pixels, x, y + 7, 20, 2, Night0);
            FillRect(_pixels, x, y - 9, 1, 18, Night0);
            FillRect(_pixels, x + 19, y - 9, 1, 18, Steel2);
            // Гніздо: колір по ободу, темна нірка всередині.
            FillRect(_pixels, x - 6, y - 7, 8, 14, t[0]);
            FillRect(_pixels, x - 6, y - 7, 8, 3, t[1]);
            FillRect(_pixels, x - 6, y + 4, 8, 3, t[2]);
            Outline(_pixels, x - 7, y - 8, 10, 16, Night0);
            Blend(_pixels, x - 9, y - 10, 14, 20, t[0], 0.10f);
            FillRect(_pixels, x - 4, y - 3, 4, 6, Night0);
            DrawSymbol(x + 5, y - 3, colorIndex, t[1], Night0);
            bool linked = false;
            for (int w = 0; w < s.SocketOf.Length; w++)
            {
                linked |= s.SocketOf[w] == slot && w != s.Dragging;
            }

            // Індикатор: гасне, поки гніздо порожнє; горить, коли дріт вставлено.
            Color32 led = linked ? Ok : C(20, 30, 36);
            FillRect(_pixels, x + 14, y - 7, 3, 3, led);
            if (linked)
            {
                Put(_pixels, x + 14, y - 7, Steel3);
                float pulse = 0.28f + 0.14f * Mathf.Sin(s.Clock * 7.8f + slot);
                Blend(_pixels, x + 11, y - 10, 9, 9, Ok, pulse);
                Blend(_pixels, x - 8, y - 10, 30, 20, Ok, 0.05f);
            }

            // Підсвітка гнізда, коли над ним тягнуть дріт.
            if (s.Dragging >= 0)
            {
                float d = Vector2.Distance(s.DragPos, RightEnd(slot, n));
                if (d < PickRadius)
                {
                    Outline(_pixels, x - 8, y - 10, 29, 20, Cyan2);
                }
            }
        }

        private void DrawSymbol(int x0, int y0, int index, Color32 on, Color32 shadow)
        {
            string[] rows = Symbols[index % Symbols.Length];
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    if (rows[r][c] == '#')
                    {
                        Put(_pixels, x0 + c + 1, y0 + r + 1, shadow);
                    }
                }
            }

            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    if (rows[r][c] == '#')
                    {
                        Put(_pixels, x0 + c, y0 + r, on);
                    }
                }
            }
        }

        /// <summary>Дріт з провисанням: контур, тіло, блік зверху, тінь знизу.</summary>
        private void DrawWire(Vector2 a, Vector2 b, int colorIndex, float sag, float clock)
        {
            Color32[] t = WireTones[colorIndex % WireTones.Length];
            int steps = Mathf.Max(8, Mathf.CeilToInt(Vector2.Distance(a, b) * 1.4f));
            Vector2[] pts = new Vector2[steps + 1];
            for (int k = 0; k <= steps; k++)
            {
                float u = k / (float)steps;
                Vector2 p = Vector2.Lerp(a, b, u);
                p.y += sag * 4f * u * (1f - u);
                pts[k] = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            }

            // Світіння дроту на платі (кольорове гало) і тінь під ним.
            for (int k = 0; k < pts.Length; k += 2)
            {
                Blend(_pixels, (int)pts[k].x - 6, (int)pts[k].y - 6, 13, 13, t[0], 0.06f);
                Blend(_pixels, (int)pts[k].x - 3, (int)pts[k].y - 3, 7, 7, t[1], 0.05f);
            }

            foreach (Vector2 p in pts)
            {
                Blend(_pixels, (int)p.x - 1, (int)p.y + 3, 5, 5, Night0, 0.45f);
            }

            // Контур, тіло з тонами за товщиною (світлий верх, темний низ).
            foreach (Vector2 p in pts)
            {
                int x = (int)p.x;
                int y = (int)p.y;
                FillRect(_pixels, x - 1, y - 4, 3, 9, Night0);
                Put(_pixels, x, y - 4, Night0);
            }

            foreach (Vector2 p in pts)
            {
                int x = (int)p.x;
                int y = (int)p.y;
                Put(_pixels, x, y - 3, t[1]);
                Put(_pixels, x, y - 2, t[1]);
                Put(_pixels, x, y - 1, t[0]);
                Put(_pixels, x, y, t[0]);
                Put(_pixels, x, y + 1, t[0]);
                Put(_pixels, x, y + 2, t[2]);
                Put(_pixels, x, y + 3, t[2]);
            }

            // Біжучий блік вздовж дроту — «струм».
            for (int lap = 0; lap < 2; lap++)
            {
                int glint = (int)(clock * 70f + colorIndex * 23 + lap * pts.Length / 2) % Mathf.Max(1, pts.Length);
                for (int g = 0; g < 9; g++)
                {
                    int idx = glint + g;
                    if (idx >= pts.Length)
                    {
                        break;
                    }

                    int x = (int)pts[idx].x;
                    int y = (int)pts[idx].y;
                    Color32 core = g > 2 && g < 7 ? Steel3 : t[1];
                    Put(_pixels, x, y - 2, core);
                    Put(_pixels, x, y - 1, core);
                    Put(_pixels, x, y, t[1]);
                }
            }
        }

        /// <summary>Штекер на кінці дроту, який тягнуть: металевий наконечник з кольоровою смугою й кільцем під пальцем.</summary>
        private void DrawTip(Vector2 p, int colorIndex, float clock)
        {
            Color32[] t = WireTones[colorIndex % WireTones.Length];
            int x = (int)p.x;
            int y = (int)p.y;
            int ring = 15 + (int)(Mathf.Sin(clock * 9f) * 1.5f);
            Blend(_pixels, x - ring / 2, y - ring / 2 + 2, ring, ring - 2, t[0], 0.16f);
            Outline(_pixels, x - ring / 2, y - ring / 2 + 2, ring, ring - 2, t[2]);
            FillRect(_pixels, x - 1, y - 5, 13, 11, Night0);
            FillRect(_pixels, x, y - 4, 11, 9, Steel1);
            FillRect(_pixels, x, y - 4, 11, 2, Steel3);
            FillRect(_pixels, x, y + 2, 11, 3, Steel0);
            FillRect(_pixels, x + 1, y - 2, 4, 5, t[0]);
            FillRect(_pixels, x + 1, y - 2, 4, 1, t[1]);
            FillRect(_pixels, x + 1, y + 2, 4, 1, t[2]);
            FillRect(_pixels, x + 8, y - 2, 3, 5, Night0);
        }

        private void DrawSubmit(HackBoardState s)
        {
            RectInt r = SubmitRect;
            bool on = s.CanSubmit && !s.Busy && s.Mode == HackMode.Puzzle;
            if (s.Mode == HackMode.Locked)
            {
                DrawLockedButton(s);
                return;
            }

            if (s.Mode == HackMode.Solved)
            {
                RectInt b = SubmitRect;
                Blend(_pixels, b.x - 4, b.y - 4, b.width + 8, b.height + 8, Ok, 0.18f);
                FillRect(_pixels, b.x, b.y, b.width, b.height, Night0);
                FillRect(_pixels, b.x + 1, b.y + 1, b.width - 2, b.height - 2, C(24, 100, 58));
                FillRect(_pixels, b.x + 1, b.y + 1, b.width - 2, 3, Ok);
                Outline(_pixels, b.x, b.y, b.width, b.height, Ok);
                return;
            }

            int shift = s.Pressed && on ? 1 : 0;
            Color32 body = on ? Cyan1 : C(20, 32, 42);
            Color32 hi = on ? Cyan3 : C(40, 56, 70);
            Color32 lo = on ? Cyan0 : C(8, 12, 18);
            if (on)
            {
                // Світіння активної кнопки: пульсація.
                float pulse = 0.14f + 0.08f * Mathf.Sin(s.Clock * 5f);
                Blend(_pixels, r.x - 4, r.y - 4, r.width + 8, r.height + 8, Cyan2, pulse);
            }

            FillRect(_pixels, r.x, r.y + shift, r.width, r.height, Night0);
            FillRect(_pixels, r.x + 1, r.y + 1 + shift, r.width - 2, r.height - 2, body);
            FillRect(_pixels, r.x + 1, r.y + 1 + shift, r.width - 2, 2, hi);
            FillRect(_pixels, r.x + 1, r.yMax - 4 + shift, r.width - 2, 3, lo);
            Outline(_pixels, r.x, r.y + shift, r.width, r.height, on ? Cyan2 : Steel0);
        }

        /// <summary>Під час блокування кнопка — вимкнена панель із заповненням-відліком: скільки лишилось чекати.</summary>
        private void DrawLockedButton(HackBoardState s)
        {
            RectInt r = SubmitRect;
            FillRect(_pixels, r.x, r.y, r.width, r.height, Night0);
            FillRect(_pixels, r.x + 1, r.y + 1, r.width - 2, r.height - 2, C(30, 14, 18));
            int fill = Mathf.RoundToInt((r.width - 4) * Mathf.Clamp01(1f - s.LockFrac));
            FillRect(_pixels, r.x + 2, r.y + 2, fill, r.height - 4, C(110, 25, 30));
            FillRect(_pixels, r.x + 2, r.y + 2, fill, 2, Alarm);
            Outline(_pixels, r.x, r.y, r.width, r.height, Alarm);
        }

        /// <summary>Іскра в момент, коли штекер потрапив у гніздо: зірка з променів, що тане.</summary>
        private void DrawSpark(HackBoardState s)
        {
            if (s.Spark <= 0f)
            {
                return;
            }

            Color32[] t = WireTones[s.SparkColor % WireTones.Length];
            int cx = (int)s.SparkPos.x;
            int cy = (int)s.SparkPos.y;
            int len = 3 + (int)((1f - s.Spark) * 9f);
            for (int d = 2; d <= len; d++)
            {
                Color32 c = d < len - 2 ? Steel3 : t[1];
                Put(_pixels, cx + d, cy, c);
                Put(_pixels, cx - d, cy, c);
                Put(_pixels, cx, cy + d, c);
                Put(_pixels, cx, cy - d, c);
                if (d % 2 == 0)
                {
                    Put(_pixels, cx + d / 2 + 1, cy + d / 2 + 1, t[0]);
                    Put(_pixels, cx - d / 2 - 1, cy + d / 2 + 1, t[0]);
                    Put(_pixels, cx + d / 2 + 1, cy - d / 2 - 1, t[0]);
                    Put(_pixels, cx - d / 2 - 1, cy - d / 2 - 1, t[0]);
                }
            }

            FillRect(_pixels, cx - 1, cy - 1, 3, 3, Steel3);
            Blend(_pixels, cx - 8, cy - 8, 17, 17, t[1], 0.25f * s.Spark);
        }

        private void DrawFlash(HackBoardState s)
        {
            if (s.Mode == HackMode.Solved)
            {
                Blend(_pixels, 1, 24, W - 2, 150, Ok, 0.10f + 0.10f * Mathf.Clamp01(s.Flash));
            }
            else if (s.Mode == HackMode.Locked)
            {
                Color32 tint = C(110, 25, 30);
                Blend(_pixels, 1, 24, W - 2, 150, tint, 0.30f + 0.10f * Mathf.Clamp01(s.Flash));
                // Збій картинки: зсунуті рядки.
                int glitchRows = s.Failed ? 7 : 3;
                for (int g = 0; g < glitchRows; g++)
                {
                    int y = 26 + _noise.Next(0, 144);
                    int shift = _noise.Next(-6, 7);
                    ShiftRow(y, shift);
                    if (g % 2 == 0)
                    {
                        FillRect(_pixels, 2 + _noise.Next(0, W - 40), y, 14 + _noise.Next(0, 26), 1, Alarm);
                    }
                }
            }

            if (s.Mode == HackMode.Solved || s.Mode == HackMode.Locked)
            {
                // Плашка під напис результату.
                Color32 edge = s.Mode == HackMode.Solved ? Ok : Alarm;
                FillRect(_pixels, 70, 32, W - 140, 28, Night0);
                FillRect(_pixels, 72, 34, W - 144, 24, C(10, 16, 26));
                Outline(_pixels, 70, 32, W - 140, 28, edge);
                Outline(_pixels, 72, 34, W - 144, 24, Night0);
            }
        }

        private void ShiftRow(int y, int shift)
        {
            if (shift == 0 || y < 0 || y >= H)
            {
                return;
            }

            int row = (H - 1 - y) * W;
            Color32[] copy = new Color32[W];
            Array.Copy(_pixels, row, copy, 0, W);
            for (int x = 2; x < W - 2; x++)
            {
                int sx = Mathf.Clamp(x - shift, 2, W - 3);
                _pixels[row + x] = copy[sx];
            }
        }

        /// <summary>Рядок «оновлення кінескопа»: тонка світла смуга біжить зверху вниз.</summary>
        private void DrawCrtBar(HackBoardState s)
        {
            int y = 24 + (int)((s.Clock * 26f) % 150f);
            Blend(_pixels, 2, y, W - 4, 2, Cyan3, 0.06f);
            Blend(_pixels, 2, y + 2, W - 4, 1, Cyan3, 0.03f);
        }

        // ------------------------------------------------------------------ примітиви малювання

        private static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        private static void Fill(Color32[] buf, Color32 c)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = c;
            }
        }

        private static void Put(Color32[] buf, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H)
            {
                return;
            }

            buf[(H - 1 - y) * W + x] = c;
        }

        private static void FillRect(Color32[] buf, int x, int y, int w, int h, Color32 c)
        {
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    Put(buf, x + i, y + j, c);
                }
            }
        }

        private static void Outline(Color32[] buf, int x, int y, int w, int h, Color32 c)
        {
            FillRect(buf, x, y, w, 1, c);
            FillRect(buf, x, y + h - 1, w, 1, c);
            FillRect(buf, x, y, 1, h, c);
            FillRect(buf, x + w - 1, y, 1, h, c);
        }

        private static void Blend(Color32[] buf, int x, int y, int w, int h, Color32 c, float alpha)
        {
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int px = x + i;
                    int py = y + j;
                    if (px < 0 || py < 0 || px >= W || py >= H)
                    {
                        continue;
                    }

                    int idx = (H - 1 - py) * W + px;
                    Color32 o = buf[idx];
                    buf[idx] = new Color32(
                        (byte)Mathf.Clamp(o.r + (c.r - o.r) * alpha, 0, 255),
                        (byte)Mathf.Clamp(o.g + (c.g - o.g) * alpha, 0, 255),
                        (byte)Mathf.Clamp(o.b + (c.b - o.b) * alpha, 0, 255),
                        255);
                }
            }
        }

        private void Disc(Vector2 p, int radius, Color32 c)
        {
            int cx = (int)p.x;
            int cy = (int)p.y;
            for (int j = -radius; j <= radius; j++)
            {
                for (int i = -radius; i <= radius; i++)
                {
                    if (i * i + j * j <= radius * radius + (radius > 1 ? 1 : 0))
                    {
                        Put(_pixels, cx + i, cy + j, c);
                    }
                }
            }
        }
    }
}
