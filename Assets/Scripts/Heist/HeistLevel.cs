using System;
using System.Collections.Generic;

namespace Banda.Heist
{
    /// <summary>
    /// Геометрія об'єкта пограбування (у пікселях світу, Y униз, тайл 16×16).
    /// Симуляція бере з рівня лише форму: стіни, укриття, двері, вихід, сейф, камери, маршрут охорони.
    /// Візуал (тайли, світло) будується окремо з цих самих даних, тож гра й картинка не розходяться.
    /// </summary>
    public sealed class HeistLevel
    {
        public const float Tile = 16f;

        public string Name = string.Empty;
        public Vec2 Size;
        /// <summary>Тверді перешкоди, за якими не сховатися (стіни, холодильники).</summary>
        public List<Box> Walls = new List<Box>();
        /// <summary>Тверді перешкоди, біля яких Тінь може сховатися (стелажі, прилавок).</summary>
        public List<Box> Covers = new List<Box>();
        public Box Door;
        /// <summary>Виходи: стоячи тут зі здобиччю — успіх.</summary>
        public List<Box> Exits = new List<Box>();
        /// <summary>Записки з цифрами комбінації (точки, біля яких Тінь «читає»).</summary>
        public List<Vec2> Notes = new List<Vec2>();
        public Vec2 SafePos;
        public List<Vec2> Cameras = new List<Vec2>();
        public List<Vec2> GuardPath = new List<Vec2>();
        public Vec2 ShadowStart;
        /// <summary>Джерела світла (лише візуал; симуляція їх не читає). Колір — hex з палітри.</summary>
        public List<LightSpot> Lights = new List<LightSpot>();

        /// <summary>
        /// «Тестовий поверх» Godot-версії (heist_sim.gd) — 1:1, для порівняльних тестів поведінки.
        /// </summary>
        public static HeistLevel GodotTestFloor()
        {
            return new HeistLevel
            {
                Name = "Тестовий поверх (Godot)",
                Size = new Vec2(320, 180),
                Walls = new List<Box>
                {
                    new Box(0, 0, 320, 4), new Box(0, 176, 320, 4), new Box(0, 0, 4, 180), new Box(316, 0, 4, 180),
                    new Box(200, 0, 4, 70), new Box(200, 100, 4, 80),
                },
                Covers = new List<Box> { new Box(70, 92, 60, 12), new Box(236, 120, 40, 12) },
                Door = new Box(200, 70, 4, 30),
                Exits = new List<Box> { new Box(4, 148, 26, 28) },
                SafePos = new Vec2(290, 30),
                Cameras = new List<Vec2> { new Vec2(110, 40), new Vec2(250, 70) },
                GuardPath = new List<Vec2> { new Vec2(40, 60), new Vec2(180, 60), new Vec2(180, 150), new Vec2(60, 150) },
                ShadowStart = new Vec2(16, 162),
            };
        }

        /// <summary>
        /// Сітка «Магазину на розі» 26×15 (docs/levels/store.md; копія — docs/levels/store_grid.txt, тест стежить, щоб не розійшлись).
        /// # стіна, . підлога, F холодильники, = вітрини, D вхідні двері, b задні двері, L двері підсобки,
        /// A B стелажі (3/4: 2 клітинки заввишки — верх і передня грань), E низький промо-стіл, K прилавок, X коробки, m стіл охорони, S сейф, k записка, c камера, o лампа.
        /// </summary>
        public static readonly string[] StoreGrid =
        {
            "##########################",
            "#FFFFFFFF....o...#XX.c..S#",
            "#................#X......#",
            "#.AAAAAAAA......c#..mk...#",
            "#.AAAAAAAAX......#.......#",
            "#.....o..........L.......#",
            "#.BBBBBBBB.......L....XX.#",
            "#.BBBBBBBBX......#.......b",
            "#............KKK.#.......b",
            "#.EEEE........kK.#..o....#",
            "#................#.......#",
            "#................#XXX....#",
            "#................#.......#",
            "#................#.......#",
            "##DDD#===========#########",
        };

        /// <summary>«Магазин на розі» з сітки StoreGrid. Tilemap рівня заповнюється з тієї самої сітки.</summary>
        public static HeistLevel CornerStore()
        {
            const float t = Tile;
            HeistLevel level = FromGrid("Магазин на розі", StoreGrid);
            // Нумерація як у плані: камера 1 — зал (ліворуч), камера 2 — підсобка.
            level.Cameras.Sort((a, b) => a.X.CompareTo(b.X));
            level.GuardPath = new List<Vec2>
            {
                Center(3, 10), Center(14, 10), Center(14, 2), Center(1, 2), Center(1, 10),
            };
            level.Exits = new List<Box>
            {
                new Box(2 * t, 13 * t, 3 * t, t), // перед скляними дверима D
                new Box(24 * t, 7 * t, t, 2 * t), // перед задніми дверима b
            };
            level.ShadowStart = Center(3, 13);
            level.Lights = new List<LightSpot>
            {
                // Лампи з сітки: між ними справжня темрява (стандарт R5).
                new LightSpot("lamp_corner", Center(13, 1), 44f, "fff2c4", 0.85f, LightFlicker.None),
                new LightSpot("lamp_aisle", Center(6, 5), 50f, "fff2c4", 0.9f, LightFlicker.None),
                new LightSpot("lamp_counter", Center(14, 8), 36f, "f7c95a", 0.85f, LightFlicker.Buzz), // над касою: світло на прилавку, а не на порожній підлозі
                // Над промо-столом біля входу: світло падає на товар «-30%», вхід лишається в напівтемряві.
                new LightSpot("lamp_promo", new Vec2(4f * t, 9.2f * t), 38f, "fff2c4", 0.75f, LightFlicker.None),
                new LightSpot("backroom_bulb", Center(20, 9), 44f, "d8962f", 0.8f, LightFlicker.Dying),
                // Холодильники світять холодним уздовж свого ряду.
                new LightSpot("fridge_a", new Vec2(3 * t, 2.4f * t), 30f, "3de0ff", 0.7f, LightFlicker.None),
                new LightSpot("fridge_b", new Vec2(7 * t, 2.4f * t), 30f, "3de0ff", 0.7f, LightFlicker.None),
                // Неон вивіски «МАРКЕТ 24» на фасаді під вітринами (StoreSign): рожеве слово й бірюзові цифри.
                // Радіус малий — світло лягає на тротуар і край вітрин, а не плямою на півзалу.
                new LightSpot("street_neon_a", new Vec2(159f, 258f), 24f, "ff3d7f", 0.6f, LightFlicker.Buzz),
                new LightSpot("street_neon_b", new Vec2(188f, 258f), 16f, "3de0ff", 0.45f, LightFlicker.None),
                // Зелені покажчики «ВИХІД» над обома виходами.
                new LightSpot("exit_front", new Vec2(3.5f * t, 13.6f * t), 22f, "5cf08a", 0.6f, LightFlicker.None),
                new LightSpot("exit_back", new Vec2(24.6f * t, 8f * t), 22f, "5cf08a", 0.6f, LightFlicker.None),
                // Екрани: каса й монітор охорони.
                new LightSpot("register", Center(14, 8), 14f, "5cf08a", 0.5f, LightFlicker.None),
                new LightSpot("monitor", Center(20, 3), 18f, "3de0ff", 0.6f, LightFlicker.Buzz),
            };
            return level;
        }

        public static Vec2 Center(int tx, int ty) => new Vec2(tx * Tile + Tile / 2f, ty * Tile + Tile / 2f);

        /// <summary>Розібрати сітку: суцільні ряди однакових клітинок зливаються в один прямокутник.</summary>
        public static HeistLevel FromGrid(string name, string[] grid)
        {
            const float t = Tile;
            HeistLevel level = new HeistLevel { Name = name, Size = new Vec2(grid[0].Length * t, grid.Length * t) };
            for (int y = 0; y < grid.Length; y++)
            {
                string row = grid[y];
                int x = 0;
                while (x < row.Length)
                {
                    char c = row[x];
                    int end = x;
                    while (end + 1 < row.Length && row[end + 1] == c)
                    {
                        end++;
                    }

                    Box run = new Box(x * t, y * t, (end - x + 1) * t, t);
                    switch (c)
                    {
                        case '#':
                        case 'F':
                        case '=':
                        case 'D':
                        case 'b':
                            level.Walls.Add(run);
                            break;
                        case 'A':
                        case 'B':
                        case 'E':
                        case 'K':
                        case 'X':
                        case 'm':
                            level.Covers.Add(run);
                            break;
                        case 'S':
                            level.Walls.Add(run); // сейф важкий — не сховатися, не пройти
                            level.SafePos = Center(x, y);
                            break;
                        case 'k':
                        case 'c':
                            for (int i = x; i <= end; i++)
                            {
                                (c == 'k' ? level.Notes : level.Cameras).Add(Center(i, y));
                            }

                            break;
                    }

                    x = end + 1;
                }
            }

            // Двері L — вертикальна смуга клітинок.
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float doorX = 0f;
            for (int y = 0; y < grid.Length; y++)
            {
                int i = grid[y].IndexOf('L');
                if (i >= 0)
                {
                    doorX = i * t;
                    minY = Math.Min(minY, y * t);
                    maxY = Math.Max(maxY, (y + 1) * t);
                }
            }

            if (minY < maxY)
            {
                level.Door = new Box(doorX, minY, t, maxY - minY);
            }

            return level;
        }
    }

    public enum LightFlicker
    {
        None,
        /// <summary>Люмінесцентна лампа гуде й зрідка просідає.</summary>
        Buzz,
        /// <summary>Лампа, що доживає: часто гасне на мить.</summary>
        Dying,
    }

    /// <summary>Точкове джерело світла рівня (координати рівня, радіус у пікселях).</summary>
    public sealed class LightSpot
    {
        public string Name;
        public Vec2 Pos;
        public float Radius;
        public string Color;
        public float Intensity;
        public LightFlicker Flicker;

        public LightSpot(string name, Vec2 pos, float radius, string color, float intensity, LightFlicker flicker)
        {
            Name = name;
            Pos = pos;
            Radius = radius;
            Color = color;
            Intensity = intensity;
            Flicker = flicker;
        }
    }
}
