using System;

namespace Banda.Heist
{
    /// <summary>
    /// 2D-вектор симуляції справи. Свій тип, а не UnityEngine.Vector2: HeistSim не залежить від рушія,
    /// тож її можна тестувати й запускати на хості без графіки. Вісь Y дивиться вниз (як у макетах і Godot).
    /// </summary>
    [Serializable]
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public static readonly Vec2 Zero = new Vec2(0f, 0f);
        public static readonly Vec2 One = new Vec2(1f, 1f);

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float Length => MathF.Sqrt(X * X + Y * Y);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.X * k, a.Y * k);

        public float DistanceTo(Vec2 other) => (other - this).Length;

        /// <summary>Обмежити довжину (Godot: limit_length).</summary>
        public Vec2 LimitLength(float max)
        {
            float length = Length;
            return length > max && length > 0f ? this * (max / length) : this;
        }

        /// <summary>Крок до цілі не далі за delta (Godot: move_toward).</summary>
        public Vec2 MoveToward(Vec2 target, float delta)
        {
            Vec2 diff = target - this;
            float length = diff.Length;
            return length <= delta || length < 1e-6f ? target : this + diff * (delta / length);
        }

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:0.#}, {Y:0.#})";
    }

    /// <summary>Прямокутник з семантикою Godot Rect2 (позиція — верхній лівий кут, Y униз).</summary>
    [Serializable]
    public readonly struct Box : IEquatable<Box>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;

        public Box(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float Right => X + W;
        public float Bottom => Y + H;
        public Vec2 Center => new Vec2(X + W / 2f, Y + H / 2f);

        /// <summary>Godot has_point: лівий/верхній край включно, правий/нижній — ні.</summary>
        public bool HasPoint(Vec2 p) => p.X >= X && p.Y >= Y && p.X < Right && p.Y < Bottom;

        /// <summary>Godot intersects (без меж): дотик краями — не перетин.</summary>
        public bool Intersects(Box b) => X < b.Right && b.X < Right && Y < b.Bottom && b.Y < Bottom;

        public Box Grow(float by) => new Box(X - by, Y - by, W + by * 2f, H + by * 2f);

        public Box Intersection(Box b)
        {
            float x = MathF.Max(X, b.X);
            float y = MathF.Max(Y, b.Y);
            float r = MathF.Min(Right, b.Right);
            float bottom = MathF.Min(Bottom, b.Bottom);
            return r <= x || bottom <= y ? new Box(x, y, 0f, 0f) : new Box(x, y, r - x, bottom - y);
        }

        public static Box Around(Vec2 center, float half) => new Box(center.X - half, center.Y - half, half * 2f, half * 2f);

        public bool Equals(Box other) => X.Equals(other.X) && Y.Equals(other.Y) && W.Equals(other.W) && H.Equals(other.H);
        public override bool Equals(object obj) => obj is Box other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, W, H);
        public override string ToString() => $"[{X:0.#},{Y:0.#} {W:0.#}×{H:0.#}]";
    }
}
