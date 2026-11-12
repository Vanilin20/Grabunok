using System;
using Banda.Heist;

namespace Banda.ArtGen
{
    /// <summary>
    /// «Магазин на розі» — вид строго згори (одна проєкція), тайл 16×16, палітра гри.
    /// Будується з тієї ж геометрії HeistLevel, що й симуляція: стіни, стелажі, двері, сейф стоять рівно там,
    /// де їх «бачить» HeistSim. Довкола рівня — поле Pad: тротуар з калюжами внизу, дахи сусідів з боків,
    /// щоб камера Тіні ніколи не показувала порожнечу.
    ///
    /// Вихід:
    ///   store_base.png — освітлюваний шар (підлога, стіни, меблі, товари);
    ///   store_glow.png — те, що світиться саме (скло холодильників, екран каси, неон у калюжах) — Unlit/адитивно.
    /// </summary>
    public static class StoreArt
    {
        public const int Pad = 48;
        private const int T = 16;

        public static (PixelCanvas Base, PixelCanvas Glow) Build(HeistLevel level)
        {
            int w = (int)level.Size.X + Pad * 2;
            int h = (int)level.Size.Y + Pad * 2;
            PixelCanvas b = new PixelCanvas(w, h);
            PixelCanvas g = new PixelCanvas(w, h);

            DrawOutside(b, g, level);
            DrawFloors(b, level);
            DrawWallShadows(b, level);
            DrawWalls(b, level);
            DrawWindows(b, g, level);
            DrawEntrance(b, g, level);
            DrawFridges(b, g, level);
            DrawShelves(b, g, level);
            DrawCounter(b, g, level);
            DrawBackroom(b, g, level);
            return (b, g);
        }

        private static int X(float levelX) => (int)levelX + Pad;
        private static int Y(float levelY) => (int)levelY + Pad;

        // --- Довкола: дахи сусідів і мокрий тротуар --------------------------------------------

        private static void DrawOutside(PixelCanvas b, PixelCanvas g, HeistLevel level)
        {
            int w = b.Width;
            int h = b.Height;
            int storeBottom = Y(level.Size.Y);
            // Дахи сусідніх будинків: гравій, шви рулонного покриття.
            for (int y = 0; y < storeBottom; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float n = PixelCanvas.Hash(x, y, 11);
                    Rgba c = n < 0.08f ? P.Night3 : n > 0.93f ? P.Night0 : P.Night2;
                    if ((y + (x / 37) * 5) % 23 == 0)
                    {
                        c = P.Night1; // шов покриття
                    }

                    b.Set(x, y, c);
                }
            }

            // Парапети сусідів вздовж нашого будинку.
            b.FillRect(Pad - 6, Pad - 6, (int)level.Size.X + 12, 3, P.Night4);
            b.FillRect(Pad - 6, Pad - 6, 3, (int)level.Size.Y + 6, P.Night4);
            b.FillRect(X(level.Size.X) + 3, Pad - 6, 3, (int)level.Size.Y + 6, P.Night4);
            // Кондиціонери й витяжки на дахах — «живі» кути кадру.
            DrawAcUnit(b, 10, 14);
            DrawAcUnit(b, X(level.Size.X) + 12, 70);
            DrawVent(b, 18, 120);
            DrawVent(b, X(level.Size.X) + 20, 150);
            DrawAcUnit(b, 200, 8);

            // Тротуар: плитка 8×8 зі швами, мокра.
            for (int y = storeBottom; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool seam = (x % 12 == 0) || ((y - storeBottom) % 12 == 0);
                    float n = PixelCanvas.Hash(x, y, 21);
                    Rgba c = seam ? P.Night2 : (n < 0.15f ? P.Night4 : P.Night3);
                    b.Set(x, y, c);
                }
            }

            // Бордюр і край дороги.
            int curb = h - 10;
            b.FillRect(0, curb, w, 2, P.Steel0);
            b.FillRect(0, curb + 2, w, h - curb - 2, P.Night1);
            for (int x = 0; x < w; x += 24)
            {
                b.FillRect(x + 6, h - 4, 12, 1, P.Steel0); // розмітка
            }

            // Калюжі: темна вода, що віддзеркалює неон вітрини (світиться в шарі glow).
            DrawPuddle(b, g, 70, storeBottom + 18, 34, 7, P.Neon1, P.Neon2);
            DrawPuddle(b, g, 230, storeBottom + 24, 26, 5, P.Cyan1, P.Cyan2);
            DrawPuddle(b, g, 330, storeBottom + 14, 18, 4, P.Neon1, P.Neon2);
            DrawPuddle(b, g, 150, storeBottom + 30, 14, 3, P.Lamp1, P.Lamp3);

            // Неонова трубка над вітриною (вид згори — яскрава смуга вздовж фасаду).
            int signY = storeBottom + 2;
            for (int x = X(4 * T); x < X(level.Size.X) - 10; x++)
            {
                bool gap = (x / 9) % 7 == 3; // трубка складена з відрізків-літер
                if (!gap)
                {
                    g.Set(x, signY, P.Neon2);
                    g.Set(x, signY + 1, P.Neon3.WithAlpha(200));
                    b.Set(x, signY + 2, P.Neon0);
                }
            }

            // Ліхтарний стовп на тротуарі.
            b.Disc(w - 30, storeBottom + 30, 3, P.Steel0);
            b.Disc(w - 30, storeBottom + 30, 1, P.Steel2);
            g.Disc(w - 30, storeBottom + 30, 1, P.Lamp4);
        }

        private static void DrawAcUnit(PixelCanvas b, int x, int y)
        {
            b.FillRect(x, y, 20, 14, P.Steel0);
            b.Rect(x, y, 20, 14, P.Night0);
            b.HLine(x + 1, x + 18, y + 1, P.Steel1);
            b.Disc(x + 10, y + 7, 5, P.Night1);
            b.Ring(x + 10, y + 7, 5, P.Steel1);
            b.Line(x + 6, y + 7, x + 14, y + 7, P.Steel1);
            b.Line(x + 10, y + 3, x + 10, y + 11, P.Steel1);
        }

        private static void DrawVent(PixelCanvas b, int x, int y)
        {
            b.FillRect(x, y, 12, 12, P.Steel0);
            b.Rect(x, y, 12, 12, P.Night0);
            for (int i = 2; i < 11; i += 2)
            {
                b.HLine(x + 2, x + 9, y + i, P.Night1);
            }
        }

        private static void DrawPuddle(PixelCanvas b, PixelCanvas g, int cx, int cy, int rx, int ry, Rgba dark, Rgba bright)
        {
            for (int y = -ry; y <= ry; y++)
            {
                for (int x = -rx; x <= rx; x++)
                {
                    float e = (x * x) / (float)(rx * rx) + (y * y) / (float)(ry * ry);
                    if (e > 1f)
                    {
                        continue;
                    }

                    b.Set(cx + x, cy + y, P.Night1);
                    // Відблиск — рвані горизонтальні смуги (брижі), яскравіші ближче до центру.
                    float n = PixelCanvas.Hash(cx + x, cy + y, 5);
                    if ((y + 100) % 2 == 0 && n > 0.35f + e * 0.5f)
                    {
                        g.Set(cx + x, cy + y, e < 0.35f ? bright.WithAlpha(170) : dark.WithAlpha(150));
                    }
                }
            }
        }

        // --- Підлога ----------------------------------------------------------------------------

        private static void DrawFloors(PixelCanvas b, HeistLevel level)
        {
            int doorX = X(level.Door.X);
            for (int y = Y(0); y < Y(level.Size.Y); y++)
            {
                for (int x = X(0); x < X(level.Size.X); x++)
                {
                    float n = PixelCanvas.Hash(x, y, 3);
                    Rgba c;
                    if (x >= doorX)
                    {
                        // Підсобка: бетон з плямами й тріщинами.
                        c = n < 0.12f ? P.Steel0 : P.Night4;
                        if (PixelCanvas.Hash(x / 6, y / 6, 9) > 0.9f && n > 0.4f)
                        {
                            c = P.Night3; // масляна пляма
                        }
                    }
                    else
                    {
                        // Зал: лінолеум «шахівниця» 8×8, затертий.
                        bool odd = ((x - X(0)) / 8 + (y - Y(0)) / 8) % 2 == 0;
                        bool grout = (x - X(0)) % 16 == 0 || (y - Y(0)) % 16 == 0;
                        c = grout ? P.Night3 : P.Night4;
                        if (odd && n < 0.35f)
                        {
                            c = P.Night5; // світліші плитки «шахівниці» — лише зерном, не суцільно
                        }

                        if (n > 0.97f)
                        {
                            c = P.Night3; // потертості
                        }
                    }

                    b.Set(x, y, c);
                }
            }

            // Тріщини в бетоні підсобки.
            b.Line(X(17 * T), Y(11 * T), X(18 * T + 5), Y(10 * T + 6), P.Night2);
            b.Line(X(18 * T + 5), Y(10 * T + 6), X(19 * T), Y(10 * T + 9), P.Night2);
            b.Line(X(19 * T + 9), Y(5 * T), X(20 * T + 4), Y(6 * T + 2), P.Night2);
            // Трап у підлозі підсобки.
            b.FillRect(X(17 * T + 4), Y(6 * T + 4), 6, 6, P.Night2);
            for (int i = 0; i < 6; i += 2)
            {
                b.HLine(X(17 * T + 4), X(17 * T + 9), Y(6 * T + 4) + i, P.Steel0);
            }

            // Сліди від мокрого взуття від входу вглиб залу (хтось заходив з дощу).
            for (int i = 0; i < 7; i++)
            {
                int fx = X(2.5f * T) + i * 9;
                int fy = Y(10.5f * T) - i * 7;
                b.FillRect(fx + (i % 2) * 3, fy, 2, 3, P.Night3);
            }
        }

        /// <summary>Стіни відкидають м'яку тінь на підлогу (2–3 px), щоб читалися як високі.</summary>
        private static void DrawWallShadows(PixelCanvas b, HeistLevel level)
        {
            foreach (Box wall in level.Walls)
            {
                ShadeAround(b, wall, 3);
            }

            foreach (Box cover in level.Covers)
            {
                ShadeAround(b, cover, 2);
            }
        }

        private static void ShadeAround(PixelCanvas b, Box box, int depth)
        {
            for (int d = 1; d <= depth; d++)
            {
                Rgba shade = P.Night0.WithAlpha((byte)(110 - d * 30));
                b.HLine(X(box.X) - d + 1, X(box.Right) + d - 1, Y(box.Bottom) + d - 1, shade);
                b.VLine(X(box.Right) + d - 1, Y(box.Y), Y(box.Bottom) + d - 1, shade);
            }
        }

        // --- Стіни ------------------------------------------------------------------------------

        private static void DrawWalls(PixelCanvas b, HeistLevel level)
        {
            for (int i = 0; i < level.Walls.Count; i++)
            {
                Box wall = level.Walls[i];
                bool fridges = Math.Abs(wall.Y - T) < 0.1f && wall.H <= T && wall.X > 0 && wall.W < level.Size.X - T;
                if (fridges)
                {
                    continue; // холодильники малюються окремо
                }

                int x0 = X(wall.X);
                int y0 = Y(wall.Y);
                int ww = (int)wall.W;
                int wh = (int)wall.H;
                // Верх стіни: темна кладка з розшивкою, світліший кант по краях.
                for (int y = y0; y < y0 + wh; y++)
                {
                    for (int x = x0; x < x0 + ww; x++)
                    {
                        int ly = y - y0;
                        int lx = x - x0 + ((ly / 4) % 2) * 4;
                        bool mortar = ly % 4 == 3 || lx % 8 == 7;
                        float n = PixelCanvas.Hash(x, y, 31);
                        Rgba c = mortar ? P.Night1 : (n < 0.2f ? P.Brick0 : P.Night2);
                        b.Set(x, y, c);
                    }
                }

                b.Rect(x0, y0, ww, wh, P.Night0);
                // Підсвічений внутрішній край (звідки світло кімнати).
                b.HLine(x0 + 1, x0 + ww - 2, y0 + wh - 2, P.Night5);
                b.VLine(x0 + 1, y0 + 1, y0 + wh - 2, P.Night4);
            }
        }

        // --- Вітрини й вхід ---------------------------------------------------------------------

        private static void DrawWindows(PixelCanvas b, PixelCanvas g, HeistLevel level)
        {
            int y0 = Y(12 * T);
            for (int tx = 4; tx < 21; tx++)
            {
                int x0 = X(tx * T);
                // Рама: сталь між шибками, скло — темне, з відблисками вуличного неону.
                b.FillRect(x0, y0 + 5, T, 6, P.Cyan0);
                b.HLine(x0, x0 + T - 1, y0 + 4, P.Steel1);
                b.HLine(x0, x0 + T - 1, y0 + 11, P.Steel1);
                if (tx % 3 == 1)
                {
                    b.FillRect(x0, y0 + 4, 2, 8, P.Steel2); // стояк рами
                }

                // Косі відблиски на склі.
                for (int i = 0; i < 6; i++)
                {
                    if (PixelCanvas.Hash(tx, i, 41) > 0.55f)
                    {
                        g.Set(x0 + 3 + i * 2, y0 + 5 + (i % 3) * 2, (tx % 2 == 0 ? P.Neon3 : P.Cyan3).WithAlpha(140));
                    }
                }
            }

            // Підвіконня всередині — дрібний мотлох: банки, листівки.
            for (int tx = 5; tx < 15; tx += 3)
            {
                b.Disc(X(tx * T + 6), y0 - 3, 1.5f, P.Lamp2);
            }
        }

        private static void DrawEntrance(PixelCanvas b, PixelCanvas g, HeistLevel level)
        {
            int x0 = X(T);
            int y0 = Y(12 * T);
            // Скляні двері з ручкою-штангою.
            b.FillRect(x0, y0 + 4, 3 * T, 8, P.Cyan0);
            b.Rect(x0, y0 + 4, 3 * T, 8, P.Steel2);
            b.VLine(x0 + 24, y0 + 4, y0 + 11, P.Steel2);
            b.HLine(x0 + 6, x0 + 18, y0 + 3, P.Steel3);
            b.HLine(x0 + 30, x0 + 42, y0 + 3, P.Steel3);

            // Килимок на вході з написом «ВХІД» (читається згори).
            Box exit = level.Exits[0];
            int mx = X(exit.X) + 2;
            int my = Y(exit.Y) + 10;
            int mw = (int)exit.W - 4;
            int mh = 18;
            b.FillRect(mx, my, mw, mh, P.Brick1);
            b.Rect(mx, my, mw, mh, P.Brick0);
            for (int y = my + 2; y < my + mh - 2; y += 2)
            {
                b.HLine(mx + 2, mx + mw - 3, y, P.Brick0.WithAlpha(120));
            }

            PixelFont.DrawCentered(b, "ВХІД", mx + mw / 2, my + 6, P.Brick3);

            // Зелений покажчик «ВИХІД» на стіні над дверима — світиться.
            int sx = X(1.6f * T);
            int sy = Y(12 * T) - 1;
            b.FillRect(sx, sy, 22, 3, P.Night0);
            g.FillRect(sx + 1, sy + 1, 20, 1, P.Ok);
        }

        // --- Холодильники -----------------------------------------------------------------------

        private static void DrawFridges(PixelCanvas b, PixelCanvas g, HeistLevel level)
        {
            int y0 = Y(T);
            for (int tx = 2; tx < 10; tx++)
            {
                int x0 = X(tx * T);
                // Кришка: нержавійка з решіткою вентиляції.
                b.FillRect(x0, y0, T, T - 3, P.Steel1);
                b.Rect(x0, y0, T, T - 3, P.Steel0);
                for (int i = 3; i < T - 4; i += 2)
                {
                    b.HLine(x0 + 3, x0 + T - 4, y0 + i, P.Steel0);
                }

                b.HLine(x0 + 1, x0 + T - 2, y0 + 1, P.Steel2);
                // Скляні дверцята дивляться в зал — яскрава холодна смуга з ручками.
                b.FillRect(x0, y0 + T - 3, T, 3, P.Cyan1);
                g.FillRect(x0 + 1, y0 + T - 3, T - 2, 2, P.Cyan2);
                g.HLine(x0 + 2, x0 + T - 3, y0 + T - 3, P.Cyan3.WithAlpha(200));
                b.VLine(x0 + 7, y0 + T - 3, y0 + T - 1, P.Steel3);
                // Наліпки виробників на кришках — різні, щоб ряд не був ідеальною сіткою.
                if (tx % 3 == 0)
                {
                    b.FillRect(x0 + 5, y0 + 4, 6, 3, P.Neon1);
                }
                else if (tx % 3 == 1)
                {
                    b.FillRect(x0 + 4, y0 + 8, 4, 2, P.Lamp2);
                }
            }

            // Один холодильник «барахлить»: світло блідіше, на склі конденсат.
            int bx = X(6 * T);
            g.FillRect(bx + 1, y0 + T - 3, T - 2, 2, P.Cyan1.WithAlpha(160));
        }

        // --- Стелажі з товаром ------------------------------------------------------------------

        private static void DrawShelves(PixelCanvas b, PixelCanvas g, HeistLevel level)
        {
            for (int i = 0; i < level.Covers.Count; i++)
            {
                Box c = level.Covers[i];
                bool backroom = c.X >= level.Door.X;
                bool counter = !backroom && c.Y >= 9 * T - 0.1f && c.W <= 3 * T + 0.1f;
                if (backroom || counter)
                {
                    continue;
                }

                int x0 = X(c.X);
                int y0 = Y(c.Y);
                int w = (int)c.W;
                int h = (int)c.H;
                // Металевий каркас і полиця.
                b.FillRect(x0, y0, w, h, P.Steel0);
                b.Rect(x0, y0, w, h, P.Night0);
                b.HLine(x0 + 1, x0 + w - 2, y0 + 1, P.Steel1);
                b.HLine(x0 + 1, x0 + w - 2, y0 + h / 2, P.Night1); // хребет подвійного стелажа
                // Товар з обох боків хребта.
                for (int side = 0; side < 2; side++)
                {
                    int rowY = side == 0 ? y0 + 2 : y0 + h / 2 + 1;
                    int seg = 0;
                    for (int x = x0 + 2; x < x0 + w - 3; seg++)
                    {
                        int kind = (int)(PixelCanvas.Hash(seg, side + i * 7, 51) * 5);
                        x += DrawProduct(b, kind, x, rowY, seg + side * 13 + i * 31);
                        x += 1;
                    }

                    // Цінники вздовж краю полиці, що дивиться в прохід.
                    int tagY = side == 0 ? y0 : y0 + h - 1;
                    for (int x = x0 + 4; x < x0 + w - 4; x += 9)
                    {
                        b.HLine(x, x + 2, tagY, P.Lamp4);
                        b.Set(x + 1, tagY, P.Alarm);
                    }
                }

                // Порожнє місце на полиці — товар розібрали (варіація замість ідеальної сітки).
                b.FillRect(x0 + w / 2 + 6, y0 + 2, 7, h / 2 - 3, P.Night1);
            }
        }

        /// <summary>Один товар згори. Повертає ширину, яку він зайняв.</summary>
        private static int DrawProduct(PixelCanvas b, int kind, int x, int y, int seed)
        {
            Rgba[] colors = { P.Neon1, P.Lamp2, P.Cyan1, P.Brick3, P.Brick2, P.Steel2, P.Lamp1, P.Night6 };
            Rgba col = colors[(int)(PixelCanvas.Hash(seed, 3, 61) * colors.Length) % colors.Length];
            switch (kind)
            {
                case 0: // банки: кружечки з обідком
                    for (int j = 0; j < 2; j++)
                    {
                        b.Disc(x + 1 + j * 3, y + 1, 1, col);
                        b.Set(x + 1 + j * 3, y + 1, P.Steel3);
                        b.Disc(x + 1 + j * 3, y + 4, 1, col);
                        b.Set(x + 1 + j * 3, y + 4, P.Steel3);
                    }

                    return 6;
                case 1: // коробки: прямокутники з кольоровою смугою
                    b.FillRect(x, y, 4, 5, col);
                    b.HLine(x, x + 3, y + 1, P.Steel3);
                    b.Rect(x, y, 4, 5, P.Night1);
                    return 4;
                case 2: // пляшки: кришечки
                    for (int j = 0; j < 3; j++)
                    {
                        b.Set(x + j * 2, y + 1, col);
                        b.Set(x + j * 2, y + 4, col);
                        b.Set(x + j * 2, y + 2, P.Cyan0);
                    }

                    return 6;
                case 3: // пачки чипсів: зім'яті подушки з блиском
                    b.FillRect(x, y, 5, 5, col);
                    b.Set(x + 1, y + 1, P.Steel3);
                    b.Set(x + 4, y + 4, P.Night1);
                    return 5;
                default: // банки консерви, складені
                    b.FillRect(x, y + 1, 3, 3, P.Steel2);
                    b.Set(x + 1, y + 2, col);
                    return 3;
            }
        }

        // --- Каса -------------------------------------------------------------------------------

        private static void DrawCounter(PixelCanvas b, PixelCanvas g, HeistLevel level)
        {
            Box c = level.Covers.Find(cv => cv.X < level.Door.X && cv.Y >= 9 * T - 0.1f && cv.W <= 3 * T + 0.1f);
            int x0 = X(c.X);
            int y0 = Y(c.Y);
            int w = (int)c.W;
            int h = (int)c.H;
            // Дерев'яна стільниця з волокнами.
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    float n = PixelCanvas.Hash(x / 3, y, 71);
                    b.Set(x, y, n < 0.3f ? P.Brick2 : P.Brick3);
                }
            }

            b.Rect(x0, y0, w, h, P.Brick0);
            b.HLine(x0 + 1, x0 + w - 2, y0 + 1, P.Brick4);
            // Касовий апарат: сталь, зелений екран (світиться).
            b.FillRect(x0 + 6, y0 + 3, 10, 9, P.Steel1);
            b.Rect(x0 + 6, y0 + 3, 10, 9, P.Night0);
            b.FillRect(x0 + 8, y0 + 4, 6, 3, P.Night1);
            g.FillRect(x0 + 8, y0 + 4, 6, 3, P.Ok.WithAlpha(210));
            g.Set(x0 + 9, y0 + 5, P.Steel3);
            for (int i = 0; i < 3; i++)
            {
                b.HLine(x0 + 8, x0 + 13, y0 + 8 + i, (i % 2 == 0) ? P.Steel2 : P.Steel0); // клавіші
            }

            // Термінал для карток — блакитний екран.
            b.FillRect(x0 + 20, y0 + 5, 5, 7, P.Night1);
            g.FillRect(x0 + 21, y0 + 6, 3, 2, P.Cyan2.WithAlpha(200));
            // Стійка з жуйками й стос газет.
            b.FillRect(x0 + 30, y0 + 3, 8, 9, P.Steel0);
            for (int i = 0; i < 4; i++)
            {
                b.FillRect(x0 + 31 + (i % 2) * 3, y0 + 4 + (i / 2) * 4, 2, 3, i % 2 == 0 ? P.Neon2 : P.Lamp3);
            }

            b.FillRect(x0 + 40, y0 + 4, 6, 8, P.Steel3);
            b.HLine(x0 + 41, x0 + 44, y0 + 6, P.Night3);
            b.HLine(x0 + 41, x0 + 44, y0 + 8, P.Night3);
            // Табурет продавця за прилавком.
            b.Disc(x0 + 12, y0 + h + 7, 3, P.Brick1);
            b.Ring(x0 + 12, y0 + h + 7, 3, P.Brick0);
        }

        // --- Підсобка ---------------------------------------------------------------------------

        private static void DrawBackroom(PixelCanvas b, PixelCanvas g, HeistLevel level)
        {
            // Коробки на стелажі (укриття в підсобці).
            Box c = level.Covers.Find(cv => cv.X >= level.Door.X);
            int x0 = X(c.X);
            int y0 = Y(c.Y);
            for (int i = 0; i < 4; i++)
            {
                int bx = x0 + i * 12;
                int bw = 11;
                Rgba body = i % 2 == 0 ? P.Lamp1 : P.Lamp0;
                b.FillRect(bx, y0, bw, (int)c.H, body);
                b.Rect(bx, y0, bw, (int)c.H, P.Brick0);
                b.VLine(bx + bw / 2, y0 + 1, y0 + (int)c.H - 2, P.Lamp2); // скотч
                b.HLine(bx + 2, bx + 5, y0 + 4, P.Night1); // маркування
            }

            // Палета й відро з мопом — нетверда обстановка.
            int px = X(19 * T);
            int py = Y(6.5f * T);
            b.FillRect(px, py, 22, 14, P.Lamp0);
            for (int i = 0; i < 22; i += 5)
            {
                b.VLine(px + i, py, py + 13, P.Brick0);
            }

            b.Disc(X(17 * T + 2), Y(3 * T + 4), 4, P.Cyan0);
            b.Ring(X(17 * T + 2), Y(3 * T + 4), 4, P.Steel1);
            b.Line(X(17 * T + 2), Y(3 * T + 4), X(17 * T + 9), Y(2 * T + 10), P.Brick3);
            // Старий стіл охорони з монітором — блакитне світіння.
            int dx = X(16 * T + 2);
            int dy = Y(9.5f * T);
            b.FillRect(dx, dy + 18, 14, 10, P.Brick1);
            b.FillRect(dx + 3, dy + 19, 8, 5, P.Night1);
            g.FillRect(dx + 4, dy + 20, 6, 3, P.Cyan1.WithAlpha(190));
        }
    }
}
