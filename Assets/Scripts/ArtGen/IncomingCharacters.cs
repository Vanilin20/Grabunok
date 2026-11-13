using System.Collections.Generic;
using System.IO;

namespace Banda.ArtGen
{
    /// <summary>
    /// Обробка сіток персонажів з PixelLab (Assets/Art/Incoming/*_pixellab.png, 8×8 клітинок по 32 px).
    /// Сітки — варіації пози, а не анімація, тож тут лише: зведення до палітри «матеріалами»
    /// (шкіра, одяг, метал → відтінковий ряд палітри зі збереженням світлотіні) і каталог для вибору кадрів.
    /// </summary>
    public static class IncomingCharacters
    {
        public const int Cell = 32;
        public const int Grid = 8;

        // Відтінкові ряди палітри від темного до світлого.
        private static readonly Rgba[] Night = { P.Night0, P.Night1, P.Night2, P.Night3, P.Night4, P.Night5, P.Night6, P.Night7 };
        private static readonly Rgba[] Teal = { P.Night0, P.Night2, P.Cyan0, P.Cyan1, P.Cyan2, P.Cyan3 };
        private static readonly Rgba[] Neon = { P.Night1, P.Neon0, P.Neon1, P.Neon2, P.Neon3 };
        private static readonly Rgba[] Skin = { P.Brick0, P.Brick1, P.Brick2, P.Brick3, P.Brick4, P.Neon3 };
        private static readonly Rgba[] Coat = { P.Night1, P.Brick0, P.Brick1, P.Neon0, P.Neon1, P.Alarm };
        private static readonly Rgba[] Steel = { P.Night2, P.Steel0, P.Steel1, P.Steel2, P.Steel3 };
        private static readonly Rgba[] Glow = { P.Night2, P.Cyan0, P.Cyan1, P.Ok, P.Cyan3 };

        /// <summary>Якір матеріалу: колір у джерелі → ряд палітри.</summary>
        internal readonly struct Anchor
        {
            public readonly Rgba Source;
            public readonly Rgba[] Ramp;

            public Anchor(string hex, Rgba[] ramp)
            {
                Source = Rgba.Hex(hex);
                Ramp = ramp;
            }
        }

        public sealed class Character
        {
            public readonly string Id;
            internal readonly Anchor[] Anchors;

            internal Character(string id, Anchor[] anchors)
            {
                Id = id;
                Anchors = anchors;
            }
        }

        // Якорі взято з гістограм сіток (найчастіші кольори кожного матеріалу).
        public static readonly Character Shadow = new Character("shadow", new[]
        {
            new Anchor("030118", Night), new Anchor("161737", Night), new Anchor("261c4a", Night),
            new Anchor("301e54", Night), new Anchor("381b48", Night), new Anchor("59355c", Night),
            new Anchor("784874", Night), new Anchor("192445", Teal), new Anchor("1d324e", Teal),
            new Anchor("243c56", Teal), new Anchor("24475b", Teal), new Anchor("c03c9a", Neon),
            new Anchor("e04ab0", Neon), new Anchor("8a5a3a", Skin), new Anchor("c08a60", Skin),
            new Anchor("c8c8d8", Steel),
        });

        public static readonly Character Guard = new Character("guard", new[]
        {
            new Anchor("090319", Night), new Anchor("150426", Night), new Anchor("30113a", Night),
            new Anchor("240b32", Night), new Anchor("441945", Night), new Anchor("184957", Teal),
            new Anchor("18535e", Teal), new Anchor("133647", Teal), new Anchor("072738", Teal),
            new Anchor("264c61", Teal), new Anchor("793969", Skin), new Anchor("6c3564", Skin),
            new Anchor("833c72", Skin), new Anchor("e070e0", Neon), new Anchor("c050c0", Neon),
            new Anchor("70f0ff", Teal), new Anchor("a0a0c0", Steel),
        });

        public static readonly Character Hacker = new Character("hacker", new[]
        {
            new Anchor("080616", Night), new Anchor("150c26", Night), new Anchor("362345", Night),
            new Anchor("281738", Night), new Anchor("472958", Night), new Anchor("46335a", Night),
            new Anchor("323856", Night), new Anchor("212946", Night), new Anchor("08121d", Night),
            new Anchor("484858", Steel), new Anchor("4b5763", Steel), new Anchor("53656d", Steel),
            new Anchor("24636f", Teal), new Anchor("2e6d7a", Teal), new Anchor("496b66", Teal),
            new Anchor("c03c9a", Neon), new Anchor("e050b0", Neon), new Anchor("40c070", Glow),
            new Anchor("60f090", Glow), new Anchor("208050", Glow),
        });

        public static readonly Character Distractor = new Character("distractor", new[]
        {
            new Anchor("150615", Night), new Anchor("0d0615", Night), new Anchor("170c22", Night),
            new Anchor("251a35", Night), new Anchor("372c4a", Night), new Anchor("243744", Teal),
            new Anchor("1b2836", Teal), new Anchor("1c3841", Teal), new Anchor("5b132c", Coat),
            new Anchor("641631", Coat), new Anchor("4d112b", Coat), new Anchor("390a24", Coat),
            new Anchor("721a33", Coat), new Anchor("851932", Coat), new Anchor("b02040", Coat),
            new Anchor("f1b499", Skin), new Anchor("ca8478", Skin), new Anchor("da8a78", Skin),
            new Anchor("fac7aa", Skin), new Anchor("8a5048", Skin), new Anchor("552b49", Neon),
        });

        public static readonly Character[] All = { Shadow, Guard, Hacker, Distractor };

        /// <summary>Ключова поза: клітинка сітки (рядок, стовпець від 0) і призначення.</summary>
        public readonly struct Key
        {
            public readonly int Row;
            public readonly int Col;
            public readonly string Pose;

            public Key(int row, int col, string pose)
            {
                Row = row;
                Col = col;
                Pose = pose;
            }
        }

        // Вибрано з каталогів Captures/characters/*_catalog.png: один дизайн у межах персонажа
        // (у Хакера — темне худі з рожевими навушниками; варіанти з бірюзовим/рожевим худі відкинуто).
        // Вигляду ззаду в сітках Охоронця, Хакера й Відволікача немає — його доведеться малювати.
        public static readonly Dictionary<string, Key[]> Keys = new Dictionary<string, Key[]>
        {
            ["shadow"] = new[]
            {
                new Key(6, 1, "front"), new Key(0, 7, "back"), new Key(0, 4, "side"), new Key(6, 5, "side_stride"),
                new Key(2, 4, "crouch"), new Key(2, 6, "reach"), new Key(5, 5, "carry"), new Key(5, 7, "back_alt"),
            },
            ["guard"] = new[]
            {
                new Key(0, 2, "front"), new Key(2, 1, "side_walk"), new Key(2, 0, "side_walk_b"), new Key(4, 0, "flashlight"),
                new Key(1, 5, "search"), new Key(7, 7, "alarm"), new Key(3, 1, "idle_b"), new Key(0, 0, "side"),
            },
            ["hacker"] = new[]
            {
                new Key(0, 5, "front"), new Key(1, 5, "front_b"), new Key(5, 0, "laptop"), new Key(6, 0, "laptop_b"),
                new Key(0, 4, "laptop_side"), new Key(2, 7, "arms_crossed"),
            },
            ["distractor"] = new[]
            {
                new Key(1, 3, "front"), new Key(0, 0, "wave"), new Key(2, 7, "talk"), new Key(0, 7, "talk_b"),
                new Key(3, 5, "tip_hat"), new Key(5, 4, "listen"),
            },
        };

        /// <summary>
        /// Записати стрічки ключових поз (32 px на кадр) в Assets/Art/Characters і огляд ×4 у Captures/characters.
        /// </summary>
        public static List<string> ExportKeys(string projectRoot)
        {
            List<string> written = new List<string>();
            string artDir = Path.Combine(projectRoot, "Assets", "Art", "Characters");
            string capDir = Path.Combine(projectRoot, "Captures", "characters");
            int maxKeys = 0;
            foreach (Key[] k in Keys.Values)
            {
                maxKeys = System.Math.Max(maxKeys, k.Length);
            }

            const int scale = 4;
            PixelCanvas overview = new PixelCanvas(maxKeys * Cell * scale, All.Length * Cell * scale);
            overview.Fill(P.Night3);
            for (int i = 0; i < All.Length; i++)
            {
                Character ch = All[i];
                string source = SourcePath(projectRoot, ch);
                if (!File.Exists(source))
                {
                    continue;
                }

                PixelCanvas grid = Quantize(PixelCanvas.Load(source), ch);
                Key[] keys = Keys[ch.Id];
                PixelCanvas strip = new PixelCanvas(keys.Length * Cell, Cell);
                for (int k = 0; k < keys.Length; k++)
                {
                    strip.Blit(CellAt(grid, keys[k].Row, keys[k].Col), k * Cell, 0);
                }

                string path = Path.Combine(artDir, ch.Id + "_keys.png");
                strip.Save(path);
                written.Add(path);

                string catalog = Path.Combine(capDir, ch.Id + "_catalog.png");
                Catalog(grid, scale).Save(catalog);
                written.Add(catalog);

                for (int y = 0; y < strip.Height; y++)
                {
                    for (int x = 0; x < strip.Width; x++)
                    {
                        Rgba c = strip.Get(x, y);
                        if (c.A != 0)
                        {
                            overview.FillRect(x * scale, (i * Cell + y) * scale, scale, scale, c);
                        }
                    }
                }
            }

            string overviewPath = Path.Combine(capDir, "keys_overview.png");
            overview.Save(overviewPath);
            written.Add(overviewPath);
            return written;
        }

        public static string SourcePath(string projectRoot, Character character) =>
            Path.Combine(projectRoot, "Assets", "Art", "Incoming", character.Id + "_pixellab.png");

        /// <summary>Уся сітка, зведена до палітри.</summary>
        public static PixelCanvas Quantize(PixelCanvas src, Character character)
        {
            PixelCanvas dst = new PixelCanvas(src.Width, src.Height);
            Dictionary<Rgba, Rgba> cache = new Dictionary<Rgba, Rgba>();
            for (int i = 0; i < src.Pixels.Length; i++)
            {
                Rgba p = src.Pixels[i];
                if (p.A < 128)
                {
                    continue;
                }

                Rgba opaque = p.WithAlpha(255);
                if (!cache.TryGetValue(opaque, out Rgba q))
                {
                    q = Map(opaque, character);
                    cache[opaque] = q;
                }

                dst.Pixels[i] = q;
            }

            return dst;
        }

        /// <summary>Клітинка сітки (рядок, стовпець від лівого верхнього кута).</summary>
        public static PixelCanvas CellAt(PixelCanvas grid, int row, int col) => grid.Crop(col * Cell, row * Cell, Cell, Cell);

        /// <summary>Каталог для вибору кадрів: сітка ×scale на темному тлі з насічками рядків/стовпців.</summary>
        public static PixelCanvas Catalog(PixelCanvas grid, int scale)
        {
            int pad = 6 * scale;
            PixelCanvas dst = new PixelCanvas(grid.Width * scale + pad, grid.Height * scale + pad);
            dst.Fill(P.Night3);
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Rgba c = grid.Get(x, y);
                    if (c.A == 0)
                    {
                        continue;
                    }

                    dst.FillRect(pad + x * scale, pad + y * scale, scale, scale, c);
                }
            }

            // Насічки: стовпець/рядок k позначено k+1 крапками — щоб посилатися на клітинку без шрифту.
            for (int k = 0; k < Grid; k++)
            {
                for (int d = 0; d <= k; d++)
                {
                    int cx = pad + k * Cell * scale + 2 * scale + d * 2 * scale;
                    dst.FillRect(cx, scale * 2, scale, scale, P.Lamp3);
                    int cy = pad + k * Cell * scale + 2 * scale + d * 2 * scale;
                    dst.FillRect(scale * 2, cy, scale, scale, P.Lamp3);
                }
            }

            return dst;
        }

        private static Rgba Map(Rgba c, Character character)
        {
            Anchor best = character.Anchors[0];
            int bestD = int.MaxValue;
            foreach (Anchor a in character.Anchors)
            {
                int d = ColorDistance(c, a.Source);
                if (d < bestD)
                {
                    bestD = d;
                    best = a;
                }
            }

            // У межах ряду: світлота важить більше за відтінок — так зберігається порядок тонів.
            Rgba pick = best.Ramp[0];
            int pickD = int.MaxValue;
            int lum = Luma(c);
            foreach (Rgba r in best.Ramp)
            {
                int dl = Luma(r) - lum;
                int d = dl * dl * 6 + ColorDistance(c, r);
                if (d < pickD)
                {
                    pickD = d;
                    pick = r;
                }
            }

            return pick;
        }

        private static int Luma(Rgba c) => (c.R * 299 + c.G * 587 + c.B * 114) / 1000;

        private static int ColorDistance(Rgba a, Rgba b)
        {
            int dr = a.R - b.R;
            int dg = a.G - b.G;
            int db = a.B - b.B;
            return dr * dr * 3 + dg * dg * 4 + db * db * 2;
        }
    }
}
