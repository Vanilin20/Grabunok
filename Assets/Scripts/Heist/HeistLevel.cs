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
        public Box Exit;
        public Vec2 SafePos;
        public List<Vec2> Cameras = new List<Vec2>();
        public List<Vec2> GuardPath = new List<Vec2>();
        public Vec2 ShadowStart;

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
                Exit = new Box(4, 148, 26, 28),
                SafePos = new Vec2(290, 30),
                Cameras = new List<Vec2> { new Vec2(110, 40), new Vec2(250, 70) },
                GuardPath = new List<Vec2> { new Vec2(40, 60), new Vec2(180, 60), new Vec2(180, 150), new Vec2(60, 150) },
                ShadowStart = new Vec2(16, 162),
            };
        }

        /// <summary>
        /// «Магазин на розі» (ТЗ: об'єкт MVP — одна камера, один охоронець, простий сейф).
        /// 22×13 тайлів. Торговий зал ліворуч, підсобка з сейфом праворуч за дверима (ними керує Хакер),
        /// вхід/вихід — скляні двері внизу ліворуч, вітрини вздовж нижньої стіни.
        /// </summary>
        public static HeistLevel CornerStore()
        {
            const float t = Tile;
            return new HeistLevel
            {
                Name = "Магазин на розі",
                Size = new Vec2(22 * t, 13 * t),
                Walls = new List<Box>
                {
                    new Box(0, 0, 22 * t, t), // верхня стіна
                    new Box(0, 12 * t, 22 * t, t), // нижня стіна з вітринами; скляні вхідні двері на 1..4 — зачинені, вихід через них
                    new Box(0, 0, t, 13 * t), new Box(21 * t, 0, t, 13 * t), // бокові стіни
                    new Box(15 * t, t, t, 4 * t), new Box(15 * t, 7 * t, t, 5 * t), // перегородка підсобки з дверима на 5..7
                    new Box(2 * t, t, 8 * t, t), // ряд холодильників під верхньою стіною
                },
                Covers = new List<Box>
                {
                    new Box(3 * t, 4 * t, 7 * t, t), // стелаж А
                    new Box(3 * t, 7 * t, 7 * t, t), // стелаж Б
                    new Box(11 * t, 9 * t, 3 * t, t), // прилавок з касою
                    new Box(17 * t, 9 * t, 3 * t, t), // стелаж з коробками в підсобці
                },
                Door = new Box(15 * t, 5 * t, t, 2 * t),
                Exit = new Box(t, 10 * t, 3 * t, 2 * t),
                SafePos = new Vec2(19 * t + 8, 2 * t + 8),
                Cameras = new List<Vec2> { new Vec2(13 * t + 8, 5 * t + 8) }, // дивиться на підхід до дверей підсобки
                GuardPath = new List<Vec2>
                {
                    new Vec2(2 * t + 8, 3 * t + 8), new Vec2(12 * t + 8, 3 * t + 8),
                    new Vec2(12 * t + 8, 8 * t + 8), new Vec2(2 * t + 8, 8 * t + 8),
                },
                ShadowStart = new Vec2(2 * t + 8, 11 * t + 8),
            };
        }
    }
}
