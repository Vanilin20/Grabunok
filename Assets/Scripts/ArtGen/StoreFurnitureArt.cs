using System.Collections.Generic;

namespace Banda.ArtGen
{
    /// <summary>
    /// Меблі «Магазину на розі» у виді 3/4: видно верх і передню грань, висота стелажа й холодильника — 2 тайли (32 px),
    /// щоб персонаж 32 px (≈27 px зросту) був нижчим за стелаж. Кожен ряд меблів малюється цілим полотном
    /// (товар не обривається на межі тайла) і ріжеться на унікальні тайли «тип_x_y» за клітинками сітки.
    /// Світло — зверху-зліва (лампи стелі): верхні кромки й ліві боки світліші, низ полиць темніший.
    /// </summary>
    public static class StoreFurnitureArt
    {
        private const int S = TileArt.S;

        /// <summary>Тонові ряди товарів: темний, основний, світлий.</summary>
        private static readonly Rgba[][] Ramps =
        {
            new[] { P.Neon0, P.Neon1, P.Neon2 },
            new[] { P.Lamp1, P.Lamp2, P.Lamp3 },
            new[] { P.Cyan0, P.Cyan1, P.Cyan2 },
            new[] { P.Brick2, P.Brick3, P.Brick4 },
            new[] { P.Brick1, P.Alarm, P.Neon3 },
            new[] { P.Steel1, P.Steel2, P.Steel3 },
            new[] { P.Cyan0, P.Ok, P.Cyan3 },
            new[] { P.Night4, P.Night6, P.Night7 },
        };

        /// <summary>Тайли меблів для сітки: ключ — назва тайла (rack_x_y, fridge_x_y, fridge_glow_x_y, promo_x_y).</summary>
        public static Dictionary<string, PixelCanvas> ForGrid(string[] grid)
        {
            Dictionary<string, PixelCanvas> d = new Dictionary<string, PixelCanvas>();
            int h = grid.Length;
            int w = grid[0].Length;
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    char c = grid[y][x];
                    int end = x;
                    while (end + 1 < w && grid[y][end + 1] == c)
                    {
                        end++;
                    }

                    int n = end - x + 1;
                    bool topOfRack = (c == 'A' || c == 'B') && (y == 0 || grid[y - 1][x] != c);
                    if (topOfRack)
                    {
                        Cut(d, Rack(n, x * 31 + y * 7), "rack", x, y);
                    }
                    else if (c == 'F')
                    {
                        // Холодильник стоїть під стіною: верхня половина малюється на клітинці стіни над ним.
                        Cut(d, Fridge(n, false), "fridge", x, y - 1);
                        Cut(d, Fridge(n, true), "fridge_glow", x, y - 1);
                    }
                    else if (c == 'E')
                    {
                        Cut(d, Promo(n), "promo", x, y);
                    }

                    x = end + 1;
                }
            }

            return d;
        }

        /// <summary>Неонова вивіска магазину на фасаді (над тротуаром): дошка з кронштейнами, трубки неону.</summary>
        public static PixelCanvas Sign(bool glow)
        {
            const string word = "MAPKET"; // латинські гліфи шрифту, що збігаються з кирилицею: «МАРКЕТ»
            int textW = PixelFont.Measure(word) + 6 + PixelFont.Measure("24");
            PixelCanvas t = new PixelCanvas(textW + 12, 15);
            if (!glow)
            {
                t.FillRect(0, 2, t.Width, 12, P.Night1);
                t.Rect(0, 2, t.Width, 12, P.Night0);
                t.HLine(1, t.Width - 2, 3, P.Night3); // кромка дошки під вуличним світлом
                for (int x = 3; x < t.Width - 3; x += 4)
                {
                    t.Set(x, 12, P.Night2); // шви дошки
                }

                t.FillRect(4, 0, 2, 3, P.Steel1); // кронштейни
                t.FillRect(t.Width - 6, 0, 2, 3, P.Steel1);
                t.Set(4, 0, P.Steel2);
                t.Set(t.Width - 6, 0, P.Steel2);
            }

            // Трубки: на базовому шарі — темне скло трубки, на шарі світіння — яскравий неон.
            Rgba tube = glow ? P.Neon2 : P.Neon1;
            Rgba digits = glow ? P.Cyan2 : P.Cyan1;
            PixelFont.Draw(t, word, 6, 5, tube);
            PixelFont.Draw(t, "24", 6 + PixelFont.Measure(word) + 6, 5, digits);
            if (glow)
            {
                // Перегоріла літера: одна трубка «К» гасне — неон без ідеальності.
                int kx = 6 + 3 * (PixelFont.GlyphW + 1);
                t.FillRect(kx, 5, 2, 3, Rgba.Clear);
            }

            return t;
        }

        /// <summary>Стельовий світильник 3/4: плафон-«тарілка» на тросі (джерело світла лампи).</summary>
        public static PixelCanvas CeilingLamp(bool glow)
        {
            PixelCanvas t = new PixelCanvas(12, 12);
            if (!glow)
            {
                t.VLine(5, 0, 4, P.Night2); // трос
                t.FillRect(2, 5, 8, 3, P.Steel1);
                t.HLine(2, 9, 5, P.Steel2);
                t.Set(2, 6, P.Steel2);
                t.HLine(1, 10, 7, P.Steel0);
                t.Set(1, 7, P.Night0);
                t.Set(10, 7, P.Night0);
            }
            else
            {
                t.HLine(3, 8, 8, P.Lamp4);
                t.HLine(4, 7, 9, P.Lamp3);
            }

            return t;
        }

        private static void Cut(Dictionary<string, PixelCanvas> d, PixelCanvas run, string kind, int x0, int y0)
        {
            int cols = run.Width / S;
            int rows = run.Height / S;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    d[$"{kind}_{x0 + c}_{y0 + r}"] = run.Crop(c * S, r * S, S, S);
                }
            }
        }

        private static float H(int x, int y, int seed) => PixelCanvas.Hash(x, y, seed);

        // --- Стелаж ------------------------------------------------------------------------------

        /// <summary>Стелаж n тайлів завширшки, 32 px заввишки: верхня полиця-кришка, 3 яруси товару, цоколь.</summary>
        private static PixelCanvas Rack(int n, int seed)
        {
            int w = n * S;
            PixelCanvas t = new PixelCanvas(w, 2 * S);
            // Задня стінка: перфопанель (дірочки сіткою) — текстура, а не пласка заливка.
            t.FillRect(2, 4, w - 4, 25, P.Night2);
            for (int y = 6; y < 29; y += 3)
            {
                for (int x = 4 + (y / 3 % 2); x < w - 4; x += 3)
                {
                    t.Set(x, y, P.Night1);
                }
            }

            // Верх (видно згори в 3/4): світла кромка, сталь, тінь під кромкою.
            t.HLine(1, w - 2, 0, P.Steel3);
            t.FillRect(1, 1, w - 2, 2, P.Steel1);
            t.HLine(1, w - 2, 1, P.Steel2);
            t.HLine(2, w - 3, 3, P.Night0);

            int[] boards = { 12, 21, 29 };
            for (int level = 0; level < boards.Length; level++)
            {
                int top = level == 0 ? 4 : boards[level - 1] + 2;
                Stock(t, 3, w - 3, top, boards[level] - 1, level, seed);
            }

            foreach (int b in boards)
            {
                t.HLine(2, w - 3, b, P.Steel2);
                t.HLine(2, w - 3, b + 1, P.Steel0);
                t.Set(2, b, P.Steel3);
            }

            // Цінники на кромках полиць під групами товару домальовує Stock; тут — цоколь.
            t.FillRect(1, 30, w - 2, 2, P.Night1);
            t.HLine(1, w - 2, 31, P.Night0);
            // Стійки по краях (ліва освітлена) і посередині кожні 2 тайли.
            for (int side = 0; side < 2; side++)
            {
                int x = side == 0 ? 0 : w - 2;
                t.FillRect(x, 0, 2, 32, P.Steel1);
                t.VLine(x, 0, 31, side == 0 ? P.Steel2 : P.Steel0);
                t.VLine(side == 0 ? 0 : w - 1, 0, 31, P.Night0);
            }

            for (int x = 2 * S; x < w - S; x += 2 * S)
            {
                t.VLine(x, 4, 29, P.Steel0);
                t.VLine(x - 1, 4, 29, P.Night1);
            }

            return t;
        }

        /// <summary>Ярус товару: групи однакових упаковок (2–4 «обличчя»), порожні місця, цінник під кожною групою.</summary>
        private static void Stock(PixelCanvas t, int x0, int x1, int top, int floor, int level, int seed)
        {
            int x = x0;
            int g = 0;
            while (x < x1 - 2)
            {
                float r = H(g, level, seed);
                if (r < 0.12f)
                {
                    x += 3 + (int)(H(g, level, seed + 1) * 4f); // розпродане місце — видно задню стінку
                    g++;
                    continue;
                }

                int kind = (int)(H(g, level, seed + 2) * 5f) % 5;
                Rgba[] ramp = Ramps[(int)(H(g, level, seed + 3) * Ramps.Length) % Ramps.Length];
                if (level == 2)
                {
                    // Нижній ярус у тіні від полиці над ним: тон на крок темніший.
                    ramp = new[] { P.Night1, ramp[0], ramp[1] };
                }

                int facings = 2 + (int)(H(g, level, seed + 4) * 3f);
                int groupStart = x;
                for (int f = 0; f < facings && x < x1 - 2; f++)
                {
                    int pw = Item(t, kind, x, floor, top, ramp, H(g * 7 + f, level, seed + 5));
                    x += pw + 1;
                }

                // Цінник: жовтий ярлик з червоною ціною на кромці полиці під групою.
                int tag = groupStart + (x - groupStart) / 2 - 1;
                if (floor + 1 < t.Height && tag + 2 < x1)
                {
                    t.HLine(tag, tag + 2, floor + 1, P.Lamp4);
                    t.Set(tag + 1, floor + 1, P.Alarm);
                }

                g++;
            }
        }

        /// <summary>Одна упаковка, що стоїть на полиці (floor — нижній рядок). Повертає ширину.</summary>
        private static int Item(PixelCanvas t, int kind, int x, int floor, int top, Rgba[] ramp, float jitter)
        {
            int room = floor - top + 1;
            switch (kind)
            {
                case 0: // пляшка: тіло, плечі, шийка з кришкою
                {
                    int bodyH = System.Math.Min(room - 2, 4 + (int)(jitter * 2f));
                    int y = floor - bodyH + 1;
                    t.FillRect(x, y, 3, bodyH, ramp[1]);
                    t.VLine(x, y, floor, ramp[2]);
                    t.VLine(x + 2, y, floor, ramp[0]);
                    t.HLine(x, x + 2, y + bodyH / 2, P.Steel3); // етикетка
                    t.Set(x + 1, y - 1, ramp[1]);
                    t.Set(x + 1, y - 2, P.Steel2); // кришка
                    return 3;
                }

                case 1: // банка
                {
                    int y = floor - 3;
                    t.FillRect(x, y, 3, 4, ramp[1]);
                    t.VLine(x, y, floor, ramp[2]);
                    t.VLine(x + 2, y, floor, ramp[0]);
                    t.HLine(x, x + 2, y, P.Steel2); // кришка банки
                    return 3;
                }

                case 2: // коробка пластівців: висока, смуга-етикетка, тінь праворуч
                {
                    int bh = System.Math.Min(room, 6 + (int)(jitter * 2f));
                    int y = floor - bh + 1;
                    t.FillRect(x, y, 4, bh, ramp[1]);
                    t.HLine(x, x + 3, y, ramp[2]);
                    t.VLine(x + 3, y, floor, ramp[0]);
                    t.HLine(x, x + 2, y + bh / 2, P.Lamp4);
                    t.Set(x + 1, y + bh / 2 + 1, ramp[0]);
                    return 4;
                }

                case 3: // пакет чипсів: заокруглений верх, блик
                {
                    int y = floor - 4;
                    t.FillRect(x, y, 4, 5, ramp[1]);
                    t.HLine(x, x + 3, y, ramp[0]); // запаяний шов
                    t.Set(x, y, P.Night2); // заокруглені кути — видно задню стінку
                    t.Set(x + 3, y, P.Night2);
                    t.Set(x + 1, y + 1, ramp[2]);
                    t.Set(x + 1, y + 2, ramp[2]);
                    t.VLine(x + 3, y + 1, floor, ramp[0]);
                    return 4;
                }

                default: // банка консерви / баночка з кришкою
                {
                    int y = floor - 2;
                    t.FillRect(x, y, 3, 3, ramp[1]);
                    t.HLine(x, x + 2, y - 1, P.Steel1);
                    t.Set(x, y - 1, P.Steel2);
                    t.Set(x, y, ramp[2]);
                    return 3;
                }
            }
        }

        // --- Холодильник -------------------------------------------------------------------------

        /// <summary>
        /// Холодильна вітрина n дверей, 32 px заввишки. glow = false — корпус, скло, пляшки (освітлюється Light2D);
        /// glow = true — власне світло всередині (LED-смуга, підсвічене скло) для адитивного шару.
        /// </summary>
        private static PixelCanvas Fridge(int n, bool glow)
        {
            int w = n * S;
            PixelCanvas t = new PixelCanvas(w, 2 * S);
            for (int door = 0; door < n; door++)
            {
                int dx = door * S;
                if (glow)
                {
                    t.HLine(dx + 2, dx + 13, 4, P.Cyan3.WithAlpha(230)); // LED-смуга під дахом
                    for (int y = 5; y < 28; y++)
                    {
                        byte a = (byte)System.Math.Max(0, 120 - (y - 5) * 4); // світло спадає донизу
                        t.HLine(dx + 2, dx + 13, y, P.Cyan2.WithAlpha(a));
                    }

                    continue;
                }

                // Корпус: дах, рама, решітка внизу.
                t.FillRect(dx, 0, S, 32, P.Steel1);
                t.HLine(dx, dx + 15, 0, P.Steel3);
                t.HLine(dx, dx + 15, 1, P.Steel2);
                t.HLine(dx, dx + 15, 3, P.Steel0);
                t.VLine(dx, 0, 31, P.Steel2);
                t.VLine(dx + 15, 0, 31, P.Night1);
                // Нутро за склом: темна бірюза, світліша вгорі біля LED.
                for (int y = 4; y < 28; y++)
                {
                    t.HLine(dx + 2, dx + 13, y, y < 9 ? P.Cyan1 : P.Cyan0);
                }

                int[] shelves = { 11, 18, 25 };
                foreach (int sy in shelves)
                {
                    t.HLine(dx + 2, dx + 13, sy + 1, P.Steel2);
                    int x = dx + 3;
                    int k = 0;
                    while (x < dx + 12)
                    {
                        float r = H(door * 13 + k, sy, 77);
                        if (r < 0.1f)
                        {
                            x += 3; // порожнє місце
                            k++;
                            continue;
                        }

                        Rgba[] ramp = Ramps[(int)(H(door * 5 + k / 2, sy, 78) * 6f) % 6];
                        int bh = 4 + (int)(H(door, sy + k, 79) * 2f);
                        t.VLine(x, sy - bh + 1, sy, ramp[1]);
                        t.VLine(x + 1, sy - bh + 1, sy, ramp[0]);
                        t.Set(x, sy - bh + 2, ramp[2]); // блик скла пляшки
                        t.Set(x, sy - bh, P.Steel2); // кришка
                        x += 3;
                        k++;
                    }
                }

                // Скло: відблиск по діагоналі й ручка (почергово ліворуч/праворуч).
                for (int i = 0; i < 14; i++)
                {
                    t.Blend(dx + 4 + i / 3, 26 - i, P.Steel3.WithAlpha(70));
                }

                int hx = door % 2 == 0 ? dx + 12 : dx + 3;
                t.VLine(hx, 13, 20, P.Steel3);
                t.VLine(hx + (door % 2 == 0 ? 1 : -1), 13, 20, P.Steel0);
                t.HLine(dx + 1, dx + 14, 28, P.Steel0);
                for (int x = dx + 2; x < dx + 14; x += 2)
                {
                    t.VLine(x, 29, 31, P.Night1); // решітка компресора
                }

                if (H(door, 0, 80) < 0.35f)
                {
                    t.FillRect(dx + 5, 6, 5, 3, P.Neon1); // наліпка «АКЦІЯ» на склі
                    t.HLine(dx + 6, dx + 8, 7, P.Lamp4);
                }
            }

            return t;
        }

        // --- Промо-стіл ----------------------------------------------------------------------------

        /// <summary>Низький промо-стіл (1 тайл): картонний фасад з табличкою, згори — піраміда банок.</summary>
        private static PixelCanvas Promo(int n)
        {
            int w = n * S;
            PixelCanvas t = new PixelCanvas(w, S);
            // Фасад столу (картон).
            t.FillRect(0, 9, w, 7, P.Lamp1);
            t.HLine(0, w - 1, 9, P.Lamp2);
            t.HLine(0, w - 1, 15, P.Lamp0);
            t.VLine(0, 9, 15, P.Lamp0);
            t.VLine(w - 1, 9, 15, P.Lamp0);
            for (int x = 6; x < w - 4; x += 9)
            {
                t.VLine(x, 10, 14, P.Lamp0); // стики картону
            }

            // Піраміда банок: три ряди, двох кольорів, верхня банка трохи зсунута.
            Rgba[] a = Ramps[0];
            Rgba[] b = Ramps[6];
            for (int row = 0; row < 3; row++)
            {
                int y = 8 - row * 3;
                for (int x = 1 + row * 2; x + 3 < w - row * 2; x += 4)
                {
                    Rgba[] ramp = (x / 4 + row) % 3 == 0 ? b : a;
                    t.FillRect(x, y - 2, 3, 3, ramp[1]);
                    t.Set(x, y - 2, ramp[2]);
                    t.HLine(x, x + 2, y - 2, P.Steel2);
                    t.VLine(x + 2, y - 1, y, ramp[0]);
                }
            }

            // Табличка «-30%» (шрифт 5×7 не влазить у 5 px фасаду — стоїть на столі, перекриває низ піраміди).
            int signW = PixelFont.Measure("-30%") + 4;
            int cx = w / 2 - signW / 2;
            t.FillRect(cx, 7, signW, 9, P.Lamp2); // картон, а не лампа: не яскравіший за гравця
            t.Rect(cx, 7, signW, 9, P.Brick1);
            PixelFont.Draw(t, "-30%", cx + 2, 8, P.Brick1);
            return t;
        }
    }
}
