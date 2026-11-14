using System;

namespace Banda.ArtGen
{
    /// <summary>
    /// Персонажі у виді строго згори, кадр 16×16. Стрічка: 4 рядки напрямків (вниз, праворуч, вгору, ліворуч),
    /// 7 стовпців (idle + 6 кадрів ходьби). Ходьба: ступні виходять з-під корпусу по черзі, руки протилежно,
    /// корпус ледь гойдається — вторинний рух (каптур/кашкет відстає на піксель).
    /// </summary>
    public static class CharacterArt
    {
        public const int Frame = 16;
        /// <summary>Стовпці: 0 — idle, 1 — idle на вдиху, 2..7 — шість кадрів ходьби.</summary>
        public const int Columns = 8;

        private sealed class Look
        {
            public Rgba Body;
            public Rgba BodyDark;
            public Rgba BodyLight;
            public Rgba Head;
            public Rgba HeadLight;
            public Rgba Feet;
            public Rgba Hands;
            public Rgba Rim;
            public bool Guard;
        }

        public static PixelCanvas Shadow()
        {
            return Sheet(new Look
            {
                Body = P.Night6,
                BodyDark = P.Night4,
                BodyLight = P.Night7,
                Head = P.Night7,
                HeadLight = P.Steel2,
                Feet = P.Night0,
                Hands = P.Brick2,
                Rim = P.Neon3, // яскравий кант — Тінь завжди фокус для свого гравця
                Guard = false,
            });
        }

        public static PixelCanvas Guard()
        {
            return Sheet(new Look
            {
                Body = P.Cyan1,
                BodyDark = P.Cyan0,
                BodyLight = P.Steel2,
                Head = P.Steel1,
                HeadLight = P.Steel2,
                Feet = P.Night0,
                Hands = P.Brick3,
                Rim = P.Night0,
                Guard = true,
            });
        }

        private static PixelCanvas Sheet(Look look)
        {
            PixelCanvas sheet = new PixelCanvas(Frame * Columns, Frame * 4);
            for (int col = 0; col < Columns; col++)
            {
                PixelCanvas down = DrawFacingDown(look, col);
                // Малюємо лише «обличчям униз» і крутимо за годинниковою: вниз → ліворуч → вгору → праворуч.
                PixelCanvas left = down.Rotate90();
                PixelCanvas up = left.Rotate90();
                PixelCanvas right = up.Rotate90();
                PixelCanvas[] dirs = { down, right, up, left };
                for (int d = 0; d < 4; d++)
                {
                    PixelCanvas f = dirs[d];
                    // Подвійний контур: темний — відділяє від підлоги, кольоровий кант зовні — читається в темряві.
                    f.Outline(P.Night0);
                    if (!look.Guard)
                    {
                        f.Outline(look.Rim);
                    }

                    sheet.Blit(f, col * Frame, d * Frame);
                }
            }

            return sheet;
        }

        /// <summary>Кадр «обличчям униз» (до низу екрана). col 0 — idle, 1 — вдих, 2..7 — ходьба.</summary>
        private static PixelCanvas DrawFacingDown(Look look, int col)
        {
            PixelCanvas f = new PixelCanvas(Frame, Frame);
            bool walking = col > 1;
            bool inhale = col == 1;
            float phase = walking ? (col - 2) / 6f * MathF.PI * 2f : 0f;
            int step = walking ? (int)MathF.Round(MathF.Sin(phase) * 1.6f) : 0; // ступні: −2..2
            int bob = walking && (col % 3 == 0) ? 1 : 0; // корпус ледь «пружинить» на кроці
            int cx = 8;
            int cy = 7 + bob;

            // Ступні виглядають з-під корпусу спереду, по черзі.
            f.FillRect(cx - 3, cy + 3 + Math.Max(0, step), 2, 2, look.Feet);
            f.FillRect(cx + 1, cy + 3 + Math.Max(0, -step), 2, 2, look.Feet);

            // Плечі/корпус: еліпс 11×7. Світло згори — верхні рядки світліші, низ у тіні.
            for (int y = -3; y <= 3; y++)
            {
                for (int x = -5; x <= 5; x++)
                {
                    if (x * x / (inhale ? 36f : 30f) + y * y / 11f > 1f)
                    {
                        continue;
                    }

                    Rgba c = y <= -2 ? look.BodyLight : y >= 2 ? look.BodyDark : look.Body;
                    f.Set(cx + x, cy + y, c);
                }
            }

            // Складки на плечах — дві короткі темні лінії.
            f.Set(cx - 3, cy - 1, look.BodyDark);
            f.Set(cx + 3, cy - 1, look.BodyDark);

            // Руки вздовж боків: махають протилежно до ступнів, кисть спереду.
            int arm = -step;
            f.FillRect(cx - 6, cy - 1 + arm, 1, 3, look.BodyDark);
            f.Set(cx - 6, cy + 2 + arm, look.Hands);
            f.FillRect(cx + 6, cy - 1 - arm, 1, 3, look.BodyDark);
            f.Set(cx + 6, cy + 2 - arm, look.Hands);

            if (look.Guard)
            {
                // Ремінь з рацією, погон, ліхтарик у правій руці (світить уперед).
                f.HLine(cx - 4, cx + 4, cy + 1, P.Night0);
                f.Set(cx - 3, cy + 1, P.Lamp2);
                f.FillRect(cx + 3, cy - 3, 2, 1, P.Lamp2);
                f.Set(cx + 6, cy + 3 - arm, P.Steel1);
                f.Set(cx + 6, cy + 4 - arm, P.Lamp4);
            }
            else
            {
                // Рюкзак Тіні за спиною й ремінь через груди.
                f.FillRect(cx - 2, cy - 5, 5, 2, look.BodyDark);
                f.HLine(cx - 2, cx + 2, cy - 5, look.Body);
                f.Line(cx - 4, cy - 2, cx + 3, cy + 2, P.Night1);
            }

            // Голова: світліша за плечі — головний орієнтир силуету згори.
            int hy = cy - 1;
            f.Disc(cx, hy, 2.5f, look.Head);
            f.Set(cx - 1, hy - 2, look.HeadLight);
            f.Set(cx, hy - 2, look.HeadLight);
            f.Set(cx - 2, hy - 1, look.HeadLight);
            if (look.Guard)
            {
                // Кашкет: козирок уперед і кокарда.
                f.HLine(cx - 2, cx + 2, hy + 2, P.Night0);
                f.HLine(cx - 1, cx + 1, hy + 3, P.Night1);
                f.Set(cx, hy, P.Lamp3);
            }
            else
            {
                // Отвір каптура спереду — темний, з блиском очей (обличчя не пласка пляма).
                f.HLine(cx - 1, cx + 1, hy + 2, P.Night0);
                f.HLine(cx - 1, cx + 1, hy + 3, P.Night0);
                f.Set(cx - 1, hy + 2, P.Neon3);
                f.Set(cx + 1, hy + 2, P.Neon3);
            }

            return f;
        }
    }

    /// <summary>Значки стану охоронця над головою: «?» — підозра, «!» — тривога.</summary>
    public static class GuardIcons
    {
        public static PixelCanvas Suspect() => Bubble(P.Lamp3, '?');

        public static PixelCanvas Alarm() => Bubble(P.Alarm, '!');

        private static PixelCanvas Bubble(Rgba color, char mark)
        {
            PixelCanvas b = new PixelCanvas(9, 11);
            b.FillRect(1, 0, 7, 9, P.Night0);
            b.FillRect(0, 1, 9, 7, P.Night0);
            b.Set(4, 9, P.Night0);
            b.Set(4, 10, P.Night0);
            PixelFont.Draw(b, mark.ToString(), 2, 1, color);
            return b;
        }
    }

    /// <summary>Динамічні предмети рівня: двері підсобки, сейф, камера.</summary>
    public static class PropArt
    {
        /// <summary>Двері 16×32: зачинені — сталева плита; відчинені — плита відкинута до стіни.</summary>
        public static PixelCanvas Door(bool open)
        {
            PixelCanvas d = new PixelCanvas(16, 32);
            if (!open)
            {
                d.FillRect(5, 0, 6, 32, P.Steel1);
                d.Rect(5, 0, 6, 32, P.Night0);
                d.VLine(6, 1, 30, P.Steel2);
                d.FillRect(8, 14, 2, 4, P.Lamp2); // ручка
                d.Set(10, 4, P.Alarm); // замок підсобки — червоний діод
            }
            else
            {
                // Відчинене полотно лежить уздовж стіни підсобки (повернуте на 90°).
                d.FillRect(5, 0, 3, 3, P.Steel0); // петля
                d.FillRect(8, 0, 8, 3, P.Steel1);
                d.Rect(8, 0, 8, 3, P.Night0);
                d.Set(14, 1, P.Ok); // замок відімкнено — зелений діод
                // Поріг.
                d.VLine(7, 3, 31, P.Night2);
            }

            return d;
        }

        /// <summary>Сейф згори 20×20: важкий корпус, диск, ручка. Відчинений — дверцята відкинуті, усередині золото.</summary>
        /// <summary>
        /// Сейф у виді 3/4, 24×28: кришка згори, фасад з фаскою, двері з колесом-кодом і штурвалом, петлі ліворуч,
        /// заклепки, ніжки. Світло зверху-зліва. Відчинений — двері відкинуті ліворуч, усередині гроші й злитки.
        /// </summary>
        public static PixelCanvas Safe(bool open)
        {
            PixelCanvas s = new PixelCanvas(24, 28);
            // Кришка (видно згори в 3/4).
            s.FillRect(1, 0, 22, 5, P.Steel1);
            s.HLine(1, 22, 0, P.Steel3);
            s.HLine(2, 21, 1, P.Steel2);
            s.HLine(1, 22, 4, P.Steel0);
            // Фасад.
            s.FillRect(1, 5, 22, 20, P.Steel0);
            s.VLine(1, 5, 24, P.Steel1); // освітлена ліва грань
            s.HLine(1, 22, 5, P.Steel1);
            s.VLine(22, 5, 24, P.Night1);
            // Ніжки й тінь під сейфом.
            s.FillRect(2, 25, 3, 2, P.Night1);
            s.FillRect(19, 25, 3, 2, P.Night1);
            s.HLine(1, 22, 27, P.Night0.WithAlpha(160));
            s.Rect(0, 0, 24, 25, P.Night0);
            foreach ((int x, int y) in new[] { (3, 7), (20, 7), (3, 22), (20, 22) })
            {
                s.Set(x, y, P.Steel2); // заклепки
                s.Set(x + 1, y + 1, P.Night1);
            }

            if (!open)
            {
                s.FillRect(5, 8, 14, 14, P.Steel1); // двері
                s.Rect(5, 8, 14, 14, P.Night0);
                s.HLine(6, 17, 9, P.Steel2);
                s.VLine(6, 9, 20, P.Steel2);
                s.FillRect(4, 10, 2, 3, P.Steel3); // петлі
                s.FillRect(4, 17, 2, 3, P.Steel3);
                s.Disc(12, 15, 4, P.Steel2); // колесо коду
                s.Ring(12, 15, 4, P.Night0);
                for (int a = 0; a < 8; a++)
                {
                    double r = a * Math.PI / 4.0;
                    s.Set(12 + (int)Math.Round(Math.Cos(r) * 3), 15 + (int)Math.Round(Math.Sin(r) * 3), P.Steel0); // поділки
                }

                s.Disc(12, 15, 1.2f, P.Steel0);
                s.Set(11, 12, P.Steel3); // відблиск на колесі
                s.Set(10, 13, P.Steel3);
                s.Set(12, 11, P.Alarm); // мітка коду
                s.Line(15, 12, 17, 10, P.Steel3); // штурвал
                s.Line(15, 18, 17, 20, P.Steel2);
            }
            else
            {
                s.FillRect(5, 8, 14, 14, P.Night0); // нутро
                s.FillRect(6, 11, 5, 2, P.Lamp3); // злитки
                s.FillRect(11, 11, 5, 2, P.Lamp2);
                s.HLine(6, 15, 11, P.Lamp4);
                s.FillRect(6, 15, 6, 4, P.Ok); // пачки грошей
                s.HLine(6, 11, 16, P.Cyan0);
                s.FillRect(13, 16, 4, 3, P.Ok);
                s.HLine(5, 18, 14, P.Steel0); // полиця
                s.FillRect(0, 7, 5, 17, P.Steel1); // дверцята відкинуті ліворуч
                s.VLine(0, 7, 23, P.Steel2);
                s.Rect(0, 7, 5, 17, P.Night0);
            }

            return s;
        }

        /// <summary>Купол камери згори 8×8 (без діода — діод окремим спрайтом, щоб блимав).</summary>
        public static PixelCanvas Camera()
        {
            PixelCanvas c = new PixelCanvas(8, 8);
            c.Disc(4, 4, 3, P.Steel1);
            c.Ring(4, 4, 3, P.Night0);
            c.Disc(4, 4, 1.4f, P.Night0);
            c.Set(3, 2, P.Steel3);
            return c;
        }

        public static PixelCanvas Dot(Rgba color)
        {
            PixelCanvas d = new PixelCanvas(2, 2);
            d.Fill(color);
            return d;
        }
    }
}
