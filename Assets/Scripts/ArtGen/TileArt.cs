using System;
using System.Collections.Generic;

namespace Banda.ArtGen
{
    /// <summary>
    /// Тайлсет «Магазину на розі» 16×16, вид строго згори, палітра гри.
    /// Кожен тайл — окремий PNG у Assets/Art/Heist/Tiles; з них редакторний інструмент робить Tile-ассети й заповнює Tilemap.
    /// Варіанти (підлога, товар, коробки) дають «неідеальну сітку»; краї стін — 16 варіантів за сусідами.
    /// Тайли з суфіксом _glow — власне світіння (екрани, скло холодильників, неон у калюжах) для Unlit-шару.
    /// </summary>
    public static class TileArt
    {
        public const int S = 16;

        public static Dictionary<string, PixelCanvas> All()
        {
            Dictionary<string, PixelCanvas> d = new Dictionary<string, PixelCanvas>();
            for (int v = 0; v < 4; v++)
            {
                d["floor_" + v] = Linoleum(v);
                d["concrete_" + v] = Concrete(v);
                d["sidewalk_" + v] = Sidewalk(v);
                d["roof_" + v] = Roof(v);
                d["asphalt_" + v] = Asphalt(v);
            }

            d["curb"] = Curb();
            d["drain"] = Drain();
            for (int mask = 0; mask < 16; mask++)
            {
                d["wall_" + mask] = Wall(mask);
            }

            d["shade_n"] = Shade(true, false);
            d["shade_w"] = Shade(false, true);
            d["shade_nw"] = Shade(true, true);
            d["window_0"] = Window(0);
            d["window_1"] = Window(1);
            d["window_glow_0"] = WindowGlow(0);
            d["window_glow_1"] = WindowGlow(1);
            d["door_front_0"] = FrontDoor(0);
            d["door_front_1"] = FrontDoor(1);
            d["door_front_2"] = FrontDoor(2);
            d["door_back_0"] = BackDoor(0);
            d["door_back_1"] = BackDoor(1);
            for (int v = 0; v < 3; v++)
            {
                d["fridge_" + v] = Fridge(v);
                d["shelf_" + v] = Shelf(v);
                d["box_" + v] = Box(v);
                d["promo_" + v] = Promo(v);
            }

            d["fridge_glow"] = FridgeGlow();
            d["shelf_end_l"] = ShelfEnd(false);
            d["shelf_end_r"] = ShelfEnd(true);
            d["counter_w"] = Counter(0);
            d["counter_register"] = Counter(1);
            d["counter_ne"] = Counter(2);
            d["counter_s"] = Counter(3);
            d["register_glow"] = RegisterGlow();
            d["desk"] = Desk();
            d["desk_glow"] = DeskGlow();
            d["mat_0"] = Mat(0);
            d["mat_1"] = Mat(1);
            d["mat_2"] = Mat(2);
            d["litter_0"] = Litter(0);
            d["litter_1"] = Litter(1);
            d["litter_2"] = Litter(2);
            d["stain"] = Stain();
            d["puddle_glow_0"] = PuddleGlow(P.Neon2, 0);
            d["puddle_glow_1"] = PuddleGlow(P.Cyan2, 1);
            d["note"] = Note();
            return d;
        }

        /// <summary>Зібрати рівень із тайлів у одне зображення (без світла) — для стрічки камери Хакера.</summary>
        public static PixelCanvas Compose(string[] grid)
        {
            Dictionary<string, PixelCanvas> tiles = All();
            PixelCanvas c = new PixelCanvas(grid[0].Length * S, grid.Length * S);
            for (int y = 0; y < grid.Length; y++)
            {
                for (int x = 0; x < grid[0].Length; x++)
                {
                    char ch = grid[y][x];
                    string tile = ch == '#' || ch == '=' || ch == 'D' || ch == 'b' ? "wall_15"
                        : ch == 'F' ? "fridge_0" : ch == 'A' || ch == 'B' ? "shelf_" + (x % 3)
                        : ch == 'E' ? "promo_0" : ch == 'K' ? "counter_register" : ch == 'X' ? "box_0"
                        : ch == 'm' ? "desk" : x > 17 ? "concrete_0" : "floor_" + ((x + y) % 4);
                    c.Blit(tiles[tile], x * S, y * S);
                }
            }

            return c;
        }

        private static PixelCanvas New() => new PixelCanvas(S, S);

        private static float H(int x, int y, int seed) => PixelCanvas.Hash(x, y, seed);

        // --- Підлоги ----------------------------------------------------------------------------

        /// <summary>Лінолеум залу: шахівниця 8×8 лише зерном (не суцільно), шов по краю тайла, варіанти зносу.</summary>
        private static PixelCanvas Linoleum(int v)
        {
            PixelCanvas t = New();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    // Шахівниця 8×8 двома близькими тонами, зерно рідке — щоб на екрані не було «сітки з крапок».
                    bool odd = (x / 8 + y / 8) % 2 == 0;
                    float n = H(x, y, 10 + v);
                    // Світлі плитки — лише наполовину зерном: шахівниця видна, але не кричить.
                    Rgba c = odd && n < 0.45f ? P.Night5 : P.Night4;
                    if (n > 0.97f)
                    {
                        c = P.Night3;
                    }

                    if (y == 15 && x % 8 != 0)
                    {
                        c = P.Night3; // лише нижній шов плити — світло падає згори
                    }

                    t.Set(x, y, c);
                }
            }

            switch (v)
            {
                case 1: // потерта смуга від візків
                    t.HLine(2, 13, 9, P.Night3);
                    t.HLine(4, 11, 10, P.Night3);
                    break;
                case 2: // скол плитки
                    t.Set(11, 4, P.Night2);
                    t.Set(12, 4, P.Night2);
                    t.Set(12, 5, P.Night3);
                    break;
                case 3: // світліша нова плита
                    for (int y = 1; y < S; y++)
                    {
                        for (int x = 1; x < S; x++)
                        {
                            if (H(x, y, 77) < 0.5f)
                            {
                                t.Set(x, y, P.Night5);
                            }
                        }
                    }

                    break;
            }

            return t;
        }

        private static PixelCanvas Concrete(int v)
        {
            PixelCanvas t = New();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float n = H(x, y, 30 + v);
                    t.Set(x, y, n < 0.12f ? P.Steel0 : n > 0.94f ? P.Night3 : P.Night4);
                }
            }

            if (v == 1)
            {
                t.Line(1, 12, 7, 8, P.Night2); // тріщина
                t.Line(7, 8, 10, 9, P.Night2);
            }
            else if (v == 2)
            {
                t.Disc(9, 6, 3, P.Night3.WithAlpha(180)); // масляна пляма
                t.Set(8, 5, P.Night5);
            }

            return t;
        }

        private static PixelCanvas Sidewalk(int v)
        {
            PixelCanvas t = New();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    bool seam = x % 8 == 0 || y % 8 == 0;
                    float n = H(x, y, 50 + v);
                    t.Set(x, y, seam ? P.Night2 : n < 0.18f ? P.Night4 : P.Night3);
                }
            }

            if (v == 2)
            {
                t.FillRect(3, 9, 5, 2, P.Night1); // калюжа (світіння — окремим тайлом)
                t.FillRect(2, 10, 7, 1, P.Night1);
            }
            else if (v == 3)
            {
                t.Set(11, 3, P.Steel2); // недопалок
                t.Set(12, 3, P.Lamp1);
            }

            return t;
        }

        private static PixelCanvas Roof(int v)
        {
            PixelCanvas t = New();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float n = H(x, y, 60 + v);
                    t.Set(x, y, n < 0.1f ? P.Night3 : n > 0.92f ? P.Night0 : P.Night2);
                }
            }

            if (v == 1)
            {
                t.HLine(0, 15, 7, P.Night1); // шов покрівлі
            }
            else if (v == 2)
            {
                t.FillRect(4, 4, 8, 8, P.Steel0); // витяжка
                t.Rect(4, 4, 8, 8, P.Night0);
                for (int i = 6; i < 11; i += 2)
                {
                    t.HLine(5, 10, i, P.Night1);
                }
            }

            return t;
        }

        private static PixelCanvas Asphalt(int v)
        {
            PixelCanvas t = New();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float n = H(x, y, 70 + v);
                    t.Set(x, y, n < 0.08f ? P.Night3 : P.Night1);
                }
            }

            if (v == 1)
            {
                t.HLine(2, 13, 8, P.Steel0); // розмітка
            }

            return t;
        }

        private static PixelCanvas Curb()
        {
            PixelCanvas t = New();
            t.FillRect(0, 0, S, S, P.Night1);
            t.FillRect(0, 0, S, 3, P.Steel0);
            t.HLine(0, 15, 0, P.Steel1);
            return t;
        }

        private static PixelCanvas Drain()
        {
            PixelCanvas t = Concrete(0);
            t.FillRect(4, 4, 8, 8, P.Night2);
            t.Rect(4, 4, 8, 8, P.Steel0);
            for (int i = 6; i < 11; i += 2)
            {
                t.HLine(5, 10, i, P.Steel0);
            }

            return t;
        }

        // --- Стіни ------------------------------------------------------------------------------

        /// <summary>
        /// Верх стіни: темна кладка-«шапка». mask — де підлога (1 N, 2 E, 4 S, 8 W): з того боку світліший кант
        /// (світло кімнати падає на край стіни).
        /// </summary>
        private static PixelCanvas Wall(int mask)
        {
            PixelCanvas t = New();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int lx = x + ((y / 4) % 2) * 4;
                    bool mortar = y % 4 == 3 || lx % 8 == 7;
                    float n = H(x, y, 80);
                    t.Set(x, y, mortar ? P.Night1 : n < 0.22f ? P.Brick0 : P.Night2);
                }
            }

            if ((mask & 1) != 0)
            {
                t.HLine(0, 15, 0, P.Night5);
                t.HLine(0, 15, 1, P.Night4);
            }

            if ((mask & 4) != 0)
            {
                t.HLine(0, 15, 15, P.Night5);
                t.HLine(0, 15, 14, P.Night4);
            }

            if ((mask & 2) != 0)
            {
                t.VLine(15, 0, 15, P.Night4);
            }

            if ((mask & 8) != 0)
            {
                t.VLine(0, 0, 15, P.Night4);
            }

            return t;
        }

        /// <summary>М'яка тінь від стіни/меблів на підлогу (накладка): з півночі, із заходу або кутом.</summary>
        private static PixelCanvas Shade(bool north, bool west)
        {
            PixelCanvas t = New();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    int d = Math.Min(north ? y : 99, west ? x : 99);
                    if (d < 4)
                    {
                        t.Set(x, y, P.Night0.WithAlpha((byte)(120 - d * 30)));
                    }
                }
            }

            return t;
        }

        private static PixelCanvas Window(int v)
        {
            PixelCanvas t = New();
            t.FillRect(0, 0, S, S, P.Night1);
            t.FillRect(0, 4, S, 8, P.Cyan0);
            t.HLine(0, 15, 3, P.Steel1);
            t.HLine(0, 15, 12, P.Steel1);
            t.HLine(0, 15, 4, P.Cyan1);
            if (v == 1)
            {
                t.FillRect(14, 3, 2, 10, P.Steel2); // стояк рами
            }

            // Підвіконня всередині — дрібний мотлох.
            if (v == 0)
            {
                t.Set(5, 1, P.Lamp2);
                t.Set(6, 1, P.Lamp1);
            }

            return t;
        }

        private static PixelCanvas WindowGlow(int v)
        {
            PixelCanvas t = New();
            for (int i = 0; i < 5; i++)
            {
                int x = 2 + i * 3 + v;
                int y = 5 + (i * 2 + v) % 5;
                t.Set(x, y, (i % 2 == 0 ? P.Neon3 : P.Cyan3).WithAlpha(150)); // відблиски вулиці на склі
            }

            return t;
        }

        private static PixelCanvas FrontDoor(int part)
        {
            PixelCanvas t = New();
            t.FillRect(0, 0, S, S, P.Night1);
            t.FillRect(0, 4, S, 8, P.Cyan0);
            t.HLine(0, 15, 3, P.Steel2);
            t.HLine(0, 15, 12, P.Steel2);
            if (part == 0)
            {
                t.VLine(0, 3, 12, P.Steel2);
                t.HLine(5, 13, 2, P.Steel3); // штанга-ручка
            }
            else if (part == 1)
            {
                t.VLine(8, 3, 12, P.Steel2); // стик стулок
            }
            else
            {
                t.VLine(15, 3, 12, P.Steel2);
                t.HLine(2, 10, 2, P.Steel3);
            }

            return t;
        }

        /// <summary>Задні двері в правій стіні: сталеве полотно з засувкою, вид згори — смуга в товщі стіни.</summary>
        private static PixelCanvas BackDoor(int part)
        {
            PixelCanvas t = Wall(8);
            t.FillRect(3, 0, 6, S, P.Steel1);
            t.VLine(3, 0, 15, P.Night0);
            t.VLine(8, 0, 15, P.Night0);
            t.VLine(4, 0, 15, P.Steel2);
            if (part == 0)
            {
                t.FillRect(1, 12, 2, 4, P.Lamp2); // засувка
            }

            return t;
        }

        // --- Меблі ------------------------------------------------------------------------------

        private static PixelCanvas Fridge(int v)
        {
            PixelCanvas t = New();
            t.FillRect(0, 0, S, S - 3, P.Steel1);
            t.Rect(0, 0, S, S - 3, P.Steel0);
            for (int i = 3; i < S - 4; i += 2)
            {
                t.HLine(3, S - 4, i, P.Steel0); // решітка
            }

            t.HLine(1, S - 2, 1, P.Steel2);
            t.FillRect(0, S - 3, S, 3, P.Cyan1);
            t.VLine(7, S - 3, S - 1, P.Steel3);
            if (v == 1)
            {
                t.FillRect(5, 4, 6, 3, P.Neon1); // наліпка
            }
            else if (v == 2)
            {
                t.FillRect(4, 8, 4, 2, P.Lamp2);
            }

            return t;
        }

        private static PixelCanvas FridgeGlow()
        {
            PixelCanvas t = New();
            t.FillRect(1, S - 3, S - 2, 2, P.Cyan2);
            t.HLine(2, S - 3, S - 3, P.Cyan3.WithAlpha(200));
            return t;
        }

        /// <summary>Подвійний стелаж згори: каркас, хребет посередині, товар з обох боків, цінники по краях.</summary>
        private static PixelCanvas Shelf(int v)
        {
            PixelCanvas t = New();
            t.FillRect(0, 1, S, S - 2, P.Steel0);
            t.HLine(0, 15, 1, P.Steel1);
            t.HLine(0, 15, 14, P.Night1);
            t.HLine(0, 15, 8, P.Night1); // хребет
            Rgba[] colors = { P.Neon1, P.Lamp2, P.Cyan1, P.Brick3, P.Steel2, P.Lamp1, P.Night6, P.Ok };
            for (int side = 0; side < 2; side++)
            {
                int y0 = side == 0 ? 3 : 10;
                int x = 1;
                int seg = 0;
                while (x < S - 2)
                {
                    int kind = (int)(H(seg, side + v * 5, 90) * 5f);
                    Rgba col = colors[(int)(H(seg, side + v * 7, 91) * colors.Length) % colors.Length];
                    x += Product(t, kind, x, y0, col) + 1;
                    seg++;
                }
            }

            if (v == 2)
            {
                t.FillRect(6, 3, 6, 4, P.Night1); // розібраний товар
            }

            // Цінники: жовті з червоною ціною, вздовж обох проходів.
            for (int x = 2; x < S - 2; x += 6)
            {
                t.HLine(x, x + 2, 0, P.Lamp4);
                t.Set(x + 1, 0, P.Alarm);
                t.HLine(x, x + 2, 15, P.Lamp4);
            }

            return t;
        }

        private static int Product(PixelCanvas t, int kind, int x, int y, Rgba col)
        {
            switch (kind)
            {
                case 0: // банки
                    t.Set(x, y, col);
                    t.Set(x + 2, y, col);
                    t.Set(x, y + 2, col);
                    t.Set(x + 2, y + 2, col);
                    t.Set(x + 1, y + 1, P.Steel3);
                    return 3;
                case 1: // коробка
                    t.FillRect(x, y, 3, 4, col);
                    t.HLine(x, x + 2, y, P.Steel3);
                    return 3;
                case 2: // пляшки
                    t.Set(x, y, col);
                    t.Set(x, y + 2, col);
                    t.Set(x + 1, y + 1, P.Cyan0);
                    return 2;
                case 3: // пачка
                    t.FillRect(x, y, 3, 3, col);
                    t.Set(x, y, P.Steel3);
                    return 3;
                default: // консерви
                    t.FillRect(x, y + 1, 2, 2, P.Steel2);
                    t.Set(x, y + 1, col);
                    return 2;
            }
        }

        private static PixelCanvas ShelfEnd(bool right)
        {
            PixelCanvas t = Shelf(0);
            int x = right ? S - 2 : 0;
            t.FillRect(x, 1, 2, S - 2, P.Steel1);
            t.VLine(right ? S - 1 : 0, 1, S - 2, P.Night0);
            return t;
        }

        /// <summary>Торцевий промо-стенд: піраміда банок газованки, картонна табличка.</summary>
        private static PixelCanvas Promo(int v)
        {
            PixelCanvas t = New();
            t.FillRect(0, 2, S, S - 4, P.Lamp0);
            t.Rect(0, 2, S, S - 4, P.Brick0);
            Rgba can = v == 1 ? P.Neon2 : v == 2 ? P.Ok : P.Alarm;
            for (int y = 4; y < 12; y += 3)
            {
                for (int x = 2; x < 14; x += 3)
                {
                    t.Set(x, y, can);
                    t.Set(x + 1, y, P.Steel3);
                }
            }

            if (v == 0)
            {
                t.FillRect(5, 0, 6, 3, P.Lamp3); // табличка «АКЦІЯ»
                t.HLine(6, 9, 1, P.Alarm);
            }

            return t;
        }

        private static PixelCanvas Counter(int part)
        {
            PixelCanvas t = New();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    t.Set(x, y, H(x / 3, y, 71) < 0.3f ? P.Brick2 : P.Brick3);
                }
            }

            t.HLine(0, 15, 0, P.Brick4);
            t.HLine(0, 15, 15, P.Brick0);
            switch (part)
            {
                case 0: // лівий край з газетами
                    t.VLine(0, 0, 15, P.Brick0);
                    t.FillRect(3, 4, 8, 8, P.Steel3);
                    t.HLine(4, 9, 6, P.Night3);
                    t.HLine(4, 9, 8, P.Night3);
                    break;
                case 1: // каса з екраном
                    t.FillRect(3, 3, 10, 10, P.Steel1);
                    t.Rect(3, 3, 10, 10, P.Night0);
                    t.FillRect(5, 4, 6, 3, P.Night1);
                    for (int i = 0; i < 3; i++)
                    {
                        t.HLine(5, 10, 8 + i, i % 2 == 0 ? P.Steel2 : P.Steel0);
                    }

                    break;
                case 2: // кут: термінал для карток і жуйки
                    t.VLine(15, 0, 15, P.Brick0);
                    t.FillRect(3, 4, 5, 7, P.Night1);
                    t.FillRect(10, 3, 4, 9, P.Steel0);
                    t.Set(11, 5, P.Neon2);
                    t.Set(12, 8, P.Lamp3);
                    break;
                default: // вертикальна частина «Г»
                    t.VLine(0, 0, 15, P.Brick0);
                    t.VLine(15, 0, 15, P.Brick0);
                    t.Disc(8, 9, 2, P.Brick1); // табурет продавця видно між стійками
                    break;
            }

            return t;
        }

        private static PixelCanvas RegisterGlow()
        {
            PixelCanvas t = New();
            t.FillRect(5, 4, 6, 3, P.Ok.WithAlpha(210));
            t.Set(6, 5, P.Steel3);
            return t;
        }

        private static PixelCanvas Desk()
        {
            PixelCanvas t = New();
            t.FillRect(0, 2, S, S - 3, P.Brick1);
            t.Rect(0, 2, S, S - 3, P.Brick0);
            t.FillRect(3, 3, 10, 7, P.Night1); // монітор
            t.Rect(3, 3, 10, 7, P.Steel0);
            t.FillRect(5, 11, 6, 2, P.Steel1); // клавіатура
            t.Set(13, 12, P.Lamp3); // кухоль
            return t;
        }

        private static PixelCanvas DeskGlow()
        {
            PixelCanvas t = New();
            t.FillRect(4, 4, 8, 5, P.Cyan1.WithAlpha(200));
            t.HLine(5, 10, 5, P.Cyan2.WithAlpha(220));
            return t;
        }

        private static PixelCanvas Box(int v)
        {
            PixelCanvas t = New();
            if (v == 2)
            {
                // Дві коробки поруч, одна відкрита.
                t.FillRect(1, 2, 7, 12, P.Lamp1);
                t.Rect(1, 2, 7, 12, P.Brick0);
                t.FillRect(9, 4, 6, 10, P.Lamp0);
                t.Rect(9, 4, 6, 10, P.Brick0);
                t.FillRect(10, 5, 4, 3, P.Night1);
                return t;
            }

            Rgba body = v == 0 ? P.Lamp1 : P.Lamp0;
            t.FillRect(1, 1, 14, 14, body);
            t.Rect(1, 1, 14, 14, P.Brick0);
            t.VLine(8, 2, 13, P.Lamp2); // скотч
            t.HLine(2, 5, 4, P.Night1); // маркування
            if (v == 1)
            {
                t.FillRect(3, 9, 4, 3, P.Steel3); // наклейка
            }

            return t;
        }

        // --- Деталі підлоги ---------------------------------------------------------------------

        /// <summary>Килимок біля вхідних дверей: три тайли з написом «ВХІД» через увесь килимок.</summary>
        private static PixelCanvas Mat(int part)
        {
            PixelCanvas full = new PixelCanvas(S * 3, S);
            full.FillRect(1, 2, S * 3 - 2, S - 4, P.Brick1);
            full.Rect(1, 2, S * 3 - 2, S - 4, P.Brick0);
            for (int y = 4; y < S - 3; y += 2)
            {
                full.HLine(3, S * 3 - 4, y, P.Brick0.WithAlpha(110));
            }

            PixelFont.DrawCentered(full, "ВХІД", S * 3 / 2, 4, P.Brick3);
            return full.Crop(part * S, 0, S, S);
        }

        private static PixelCanvas Litter(int v)
        {
            PixelCanvas t = New();
            switch (v)
            {
                case 0: // чек
                    t.FillRect(5, 6, 3, 5, P.Steel3);
                    t.Set(6, 8, P.Steel1);
                    break;
                case 1: // обгортка
                    t.FillRect(9, 4, 3, 2, P.Neon1);
                    t.Set(10, 5, P.Lamp3);
                    break;
                default: // мокрі сліди
                    t.FillRect(3, 10, 2, 3, P.Night2);
                    t.FillRect(8, 4, 2, 3, P.Night2);
                    break;
            }

            return t;
        }

        private static PixelCanvas Stain()
        {
            PixelCanvas t = New();
            t.Disc(8, 8, 4, P.Night2.WithAlpha(150));
            t.Disc(10, 7, 2, P.Night2.WithAlpha(120));
            return t;
        }

        private static PixelCanvas PuddleGlow(Rgba color, int v)
        {
            PixelCanvas t = New();
            for (int y = 8; y < 12; y++)
            {
                for (int x = 2; x < 10; x++)
                {
                    if (y % 2 == 0 && H(x, y, 95 + v) > 0.4f)
                    {
                        t.Set(x, y, color.WithAlpha(150));
                    }
                }
            }

            return t;
        }

        /// <summary>Записка з цифрами: жовтий клаптик паперу з рядком-каракулею.</summary>
        private static PixelCanvas Note()
        {
            PixelCanvas t = New();
            t.FillRect(5, 5, 6, 6, P.Lamp3);
            t.HLine(5, 10, 5, P.Lamp4);
            t.HLine(6, 9, 7, P.Night3);
            t.HLine(6, 8, 9, P.Night3);
            t.Set(10, 10, P.Lamp1);
            return t;
        }
    }
}
