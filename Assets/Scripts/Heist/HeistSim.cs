using System;
using System.Collections.Generic;
using Banda.Core;

namespace Banda.Heist
{
    /// <summary>
    /// Авторитетна симуляція справи. Живе ТІЛЬКИ на хості; чистий C# без рушія й без мережі.
    /// Перенесено з Godot (scripts/systems/heist_sim.gd) 1:1 за поведінкою; відхилення позначені «ВІДХИЛЕННЯ».
    /// Числа тривоги — з ТЗ за змістом (росте від помилок, 50% — охорона посилюється, 100% — провал),
    /// конкретні значення тестові, як і в Godot-версії.
    /// </summary>
    public sealed class HeistSim
    {
        public const float Duration = 600f;
        public const float ShadowSpeed = 55f;
        public const float ShadowHalf = 3f; // половина розміру хітбокса Тіні
        public const float ShadowViewRadius = 52f; // «невелике коло навколо себе»
        public const float HideDistance = 12f;
        public const float TakeDistance = 18f;
        public const float CameraRange = 34f;
        public const float CameraOffTime = 6f; // «вимикає камери на кілька секунд»
        public const float GuardRange = 24f;
        public const float GuardSpeed = 26f;
        public const float GuardAlertSpeedMult = 1.5f; // після 50% тривоги
        public const float AlarmCameraPerSec = 30f;
        public const float AlarmGuardPerSec = 45f;
        public const float AlarmSafeMistake = 10f;
        public const float AlarmAlertLevel = 50f;
        public const float BusyWindow = 2.5f;
        public const int ComboRange = 40; // цифри на диску сейфа: 0..39
        public const int ComboLength = 3;

        // ВІДХИЛЕННЯ від Godot: окрім кола 24 px навколо охоронця, Тінь помічає ліхтарик охоронця —
        // конус уперед за рухом. ТЗ/стандарт «світло = геймплей»: що видно в конусі, те й бачить охоронець.
        // Конус не просвічує стіни й стелажі.
        public const float FlashlightRange = 56f;
        public const float FlashlightHalfAngleDeg = 30f;

        private readonly HeistLevel _level;
        private readonly List<float> _camerasOff = new List<float>();
        private readonly Dictionary<RoleId, float> _lastAction = new Dictionary<RoleId, float>();
        private int _guardTarget = 1;
        private float _elapsed;

        public HeistSim(HeistLevel level, int seed)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            Random rng = new Random(seed);
            Combo = new int[ComboLength];
            for (int i = 0; i < ComboLength; i++)
            {
                Combo[i] = rng.Next(0, ComboRange);
            }

            foreach (Vec2 _ in level.Cameras)
            {
                _camerasOff.Add(0f);
            }

            GuardPos = level.GuardPath.Count > 0 ? level.GuardPath[0] : Vec2.Zero;
            _guardTarget = level.GuardPath.Count > 1 ? 1 : 0;
            ShadowPos = level.ShadowStart;
        }

        public HeistLevel Level => _level;
        public float TimeLeft { get; private set; } = Duration;
        public float Alarm { get; private set; }
        public float MaxAlarm { get; private set; }
        public HeistOutcome Outcome { get; private set; } = HeistOutcome.None;
        public bool IsFinished => Outcome != HeistOutcome.None;
        public bool Alert => Alarm >= AlarmAlertLevel;

        public int[] Combo { get; }
        public bool SafeOpen { get; private set; }
        public int SafeMistakes { get; private set; }
        public bool DoorOpen { get; private set; }

        public Vec2 GuardPos { get; private set; }
        /// <summary>Напрям погляду охоронця (радіани, Y униз), за рухом.</summary>
        public float GuardFacing { get; private set; }

        public Vec2 ShadowPos { get; private set; }
        public Vec2 ShadowInput { get; private set; }
        public bool ShadowHidden { get; private set; }
        public bool ShadowCarrying { get; private set; }
        public bool ShadowSeen { get; private set; }

        public float CameraOffLeft(int index) => _camerasOff[index];

        public void Tick(float delta)
        {
            if (IsFinished)
            {
                return;
            }

            _elapsed += delta;
            TimeLeft = MathF.Max(TimeLeft - delta, 0f);
            for (int i = 0; i < _camerasOff.Count; i++)
            {
                _camerasOff[i] = MathF.Max(_camerasOff[i] - delta, 0f);
            }

            MoveGuard(delta);
            MoveShadow(delta);
            Detect(delta);
            if (Alarm >= 100f)
            {
                Outcome = HeistOutcome.Caught;
            }
            else if (TimeLeft <= 0f)
            {
                Outcome = HeistOutcome.Timeout;
            }
            else if (ShadowCarrying && AtExit(ShadowPos))
            {
                Outcome = HeistOutcome.Success;
            }
        }

        // --- Дії гравців (хост уже перевірив, що роль належить гравцю) ------------------------

        /// <summary>Застосувати дію ролі. Повертає false, якщо дія не дозволена цій ролі чи стану.</summary>
        public bool Apply(RoleId role, HeistAction action)
        {
            if (IsFinished || action == null)
            {
                return false;
            }

            switch (role)
            {
                case RoleId.Hacker:
                    return HackerAction(action);
                case RoleId.Shadow:
                    return ShadowAction(action);
                case RoleId.Safecracker:
                    return SafecrackerAction(action);
                default:
                    return false;
            }
        }

        private bool HackerAction(HeistAction action)
        {
            switch (action.Kind)
            {
                case HeistActionKind.CameraOff:
                    if (action.Index < 0 || action.Index >= _camerasOff.Count)
                    {
                        return false;
                    }

                    _camerasOff[action.Index] = CameraOffTime;
                    MarkBusy(RoleId.Hacker);
                    return true;
                case HeistActionKind.DoorToggle:
                    // Не закриваємо двері на Тіні — інакше вона застрягне в стіні.
                    if (DoorOpen && _level.Door.Grow(ShadowHalf).HasPoint(ShadowPos))
                    {
                        return false;
                    }

                    DoorOpen = !DoorOpen;
                    MarkBusy(RoleId.Hacker);
                    return true;
                default:
                    return false;
            }
        }

        private bool ShadowAction(HeistAction action)
        {
            switch (action.Kind)
            {
                case HeistActionKind.Move:
                    ShadowInput = action.Direction.LimitLength(1f);
                    if (ShadowInput.Length > 0.1f)
                    {
                        MarkBusy(RoleId.Shadow);
                    }

                    return true;
                case HeistActionKind.Hide:
                    ShadowHidden = !ShadowHidden && NearCover(ShadowPos);
                    MarkBusy(RoleId.Shadow);
                    return true;
                case HeistActionKind.Take:
                    if (SafeOpen && !ShadowCarrying && ShadowPos.DistanceTo(_level.SafePos) <= TakeDistance)
                    {
                        ShadowCarrying = true;
                        MarkBusy(RoleId.Shadow);
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        private bool SafecrackerAction(HeistAction action)
        {
            if (action.Kind != HeistActionKind.SafeTry || SafeOpen)
            {
                return false;
            }

            MarkBusy(RoleId.Safecracker);
            int[] numbers = action.Numbers ?? Array.Empty<int>();
            bool correct = numbers.Length == Combo.Length;
            for (int i = 0; correct && i < Combo.Length; i++)
            {
                correct = numbers[i] == Combo[i];
            }

            if (correct)
            {
                SafeOpen = true;
            }
            else
            {
                SafeMistakes++;
                AddAlarm(AlarmSafeMistake);
            }

            return true;
        }

        // --- Симуляція --------------------------------------------------------------------------

        private void MoveGuard(float delta)
        {
            if (_level.GuardPath.Count == 0)
            {
                return;
            }

            float speed = GuardSpeed * (Alert ? GuardAlertSpeedMult : 1f);
            Vec2 target = _level.GuardPath[_guardTarget];
            Vec2 before = GuardPos;
            GuardPos = GuardPos.MoveToward(target, speed * delta);
            Vec2 moved = GuardPos - before;
            if (moved.Length > 1e-4f)
            {
                GuardFacing = MathF.Atan2(moved.Y, moved.X);
            }

            if (GuardPos.DistanceTo(target) < 0.5f)
            {
                _guardTarget = (_guardTarget + 1) % _level.GuardPath.Count;
            }
        }

        private void MoveShadow(float delta)
        {
            if (ShadowHidden)
            {
                if (ShadowInput.Length > 0.3f)
                {
                    ShadowHidden = false; // рух виводить зі схованки
                }
                else
                {
                    return;
                }
            }

            Vec2 step = ShadowInput * (ShadowSpeed * delta);
            // Рух по осях окремо — Тінь ковзає вздовж стін, а не прилипає.
            Vec2 next = ShadowPos + new Vec2(step.X, 0f);
            if (!Blocked(next))
            {
                ShadowPos = next;
            }

            next = ShadowPos + new Vec2(0f, step.Y);
            if (!Blocked(next))
            {
                ShadowPos = next;
            }
        }

        private bool Blocked(Vec2 pos)
        {
            Box box = Box.Around(pos, ShadowHalf);
            foreach (Box rect in SolidRects())
            {
                if (rect.Intersects(box))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<Box> SolidRects()
        {
            foreach (Box wall in _level.Walls)
            {
                yield return wall;
            }

            foreach (Box cover in _level.Covers)
            {
                yield return cover;
            }

            if (!DoorOpen)
            {
                yield return _level.Door;
            }
        }

        public bool AtExit(Vec2 pos)
        {
            foreach (Box exit in _level.Exits)
            {
                if (exit.HasPoint(pos))
                {
                    return true;
                }
            }

            return false;
        }

        public bool NearCover(Vec2 pos)
        {
            foreach (Box rect in _level.Covers)
            {
                if (rect.Grow(HideDistance).HasPoint(pos))
                {
                    return true;
                }
            }

            return false;
        }

        private void Detect(float delta)
        {
            ShadowSeen = false;
            if (ShadowHidden)
            {
                return;
            }

            for (int i = 0; i < _level.Cameras.Count; i++)
            {
                if (_camerasOff[i] <= 0f && ShadowPos.DistanceTo(_level.Cameras[i]) <= CameraRange)
                {
                    ShadowSeen = true;
                    AddAlarm(AlarmCameraPerSec * delta);
                }
            }

            if (ShadowPos.DistanceTo(GuardPos) <= GuardRange || InFlashlight(ShadowPos))
            {
                ShadowSeen = true;
                AddAlarm(AlarmGuardPerSec * delta);
            }
        }

        /// <summary>Чи потрапляє точка в конус ліхтарика охоронця з урахуванням перешкод.</summary>
        public bool InFlashlight(Vec2 point)
        {
            Vec2 diff = point - GuardPos;
            float distance = diff.Length;
            if (distance > FlashlightRange || distance < 1e-4f)
            {
                return distance < 1e-4f;
            }

            float angle = MathF.Atan2(diff.Y, diff.X) - GuardFacing;
            while (angle > MathF.PI)
            {
                angle -= 2f * MathF.PI;
            }

            while (angle < -MathF.PI)
            {
                angle += 2f * MathF.PI;
            }

            if (MathF.Abs(angle) > FlashlightHalfAngleDeg * MathF.PI / 180f)
            {
                return false;
            }

            foreach (Box rect in SolidRects())
            {
                if (SegmentHitsBox(GuardPos, point, rect))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Перетин відрізка з прямокутником (метод Ліанга — Барскі).</summary>
        private static bool SegmentHitsBox(Vec2 a, Vec2 b, Box box)
        {
            float t0 = 0f;
            float t1 = 1f;
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float[] p = { -dx, dx, -dy, dy };
            float[] q = { a.X - box.X, box.Right - a.X, a.Y - box.Y, box.Bottom - a.Y };
            for (int i = 0; i < 4; i++)
            {
                if (MathF.Abs(p[i]) < 1e-6f)
                {
                    if (q[i] < 0f)
                    {
                        return false;
                    }

                    continue;
                }

                float r = q[i] / p[i];
                if (p[i] < 0f)
                {
                    t0 = MathF.Max(t0, r);
                }
                else
                {
                    t1 = MathF.Min(t1, r);
                }

                if (t0 > t1)
                {
                    return false;
                }
            }

            return true;
        }

        private void AddAlarm(float amount)
        {
            Alarm = Math.Clamp(Alarm + amount, 0f, 100f);
            MaxAlarm = MathF.Max(MaxAlarm, Alarm);
        }

        private void MarkBusy(RoleId role)
        {
            _lastAction[role] = _elapsed;
        }

        // --- Що бачить кожна роль ---------------------------------------------------------------

        /// <summary>Статичні дані, які роль отримує один раз на початку справи (і після перепідключення).</summary>
        public RoleStatic StaticViewFor(RoleId role)
        {
            switch (role)
            {
                case RoleId.Hacker:
                    return new HackerStatic
                    {
                        Size = _level.Size,
                        Walls = new List<Box>(_level.Walls),
                        Covers = new List<Box>(_level.Covers),
                        Door = _level.Door,
                        Cameras = new List<Vec2>(_level.Cameras),
                        CameraRange = CameraRange,
                        CameraOffTime = CameraOffTime,
                        Safe = _level.SafePos,
                        Exits = new List<Box>(_level.Exits),
                    };
                case RoleId.Shadow:
                    return new ShadowStatic { ViewRadius = ShadowViewRadius };
                case RoleId.Safecracker:
                    // Комбінацію Зломщик «відчуває» на своєму телефоні — її бачить лише ця роль.
                    return new SafecrackerStatic { Combo = (int[])Combo.Clone(), Range = ComboRange };
                default:
                    return null;
            }
        }

        /// <summary>Дані, які роль отримує щотіку.</summary>
        public RoleView ViewFor(RoleId role)
        {
            switch (role)
            {
                case RoleId.Hacker:
                    return new HackerView
                    {
                        CamerasOff = new List<float>(_camerasOff),
                        DoorOpen = DoorOpen,
                        Guard = GuardPos,
                        GuardFacing = GuardFacing,
                        Alert = Alert,
                    };
                case RoleId.Shadow:
                    return BuildShadowView();
                case RoleId.Safecracker:
                    return new SafecrackerView { Open = SafeOpen, Mistakes = SafeMistakes };
                default:
                    return null;
            }
        }

        /// <summary>Тінь отримує лише те, що в колі навколо неї.</summary>
        private ShadowView BuildShadowView()
        {
            float r = ShadowViewRadius;
            Box area = Box.Around(ShadowPos, r);
            ShadowView view = new ShadowView
            {
                Position = ShadowPos,
                Hidden = ShadowHidden,
                Carrying = ShadowCarrying,
                Seen = ShadowSeen,
                CanHide = NearCover(ShadowPos),
                CanTake = SafeOpen && !ShadowCarrying && ShadowPos.DistanceTo(_level.SafePos) <= TakeDistance,
            };
            foreach (Box rect in _level.Walls)
            {
                if (rect.Intersects(area))
                {
                    view.Walls.Add(rect.Intersection(area));
                }
            }

            foreach (Box rect in _level.Covers)
            {
                if (rect.Intersects(area))
                {
                    view.Covers.Add(rect);
                }
            }

            foreach (Vec2 cam in _level.Cameras)
            {
                if (ShadowPos.DistanceTo(cam) <= r)
                {
                    view.Cameras.Add(cam);
                }
            }

            if (ShadowPos.DistanceTo(GuardPos) <= r)
            {
                view.GuardVisible = true;
                view.Guard = GuardPos;
                view.GuardFacing = GuardFacing;
            }

            if (_level.Door.Intersects(area))
            {
                view.DoorVisible = true;
                view.Door = _level.Door;
                view.DoorOpen = DoorOpen;
            }

            if (ShadowPos.DistanceTo(_level.SafePos) <= r)
            {
                view.SafeVisible = true;
                view.Safe = _level.SafePos;
                view.SafeOpen = SafeOpen;
            }

            foreach (Box exit in _level.Exits)
            {
                if (exit.Intersects(area))
                {
                    view.Exits.Add(exit);
                }
            }

            return view;
        }

        /// <summary>Стан гравця для спільної панелі.</summary>
        public PlayerStatus StatusFor(RoleId role)
        {
            if (role == RoleId.Shadow && ShadowSeen)
            {
                return PlayerStatus.Danger;
            }

            return _lastAction.TryGetValue(role, out float at) && _elapsed - at <= BusyWindow ? PlayerStatus.Busy : PlayerStatus.Waiting;
        }

        public HeistSummary Summary()
        {
            return new HeistSummary
            {
                Outcome = Outcome,
                MaxAlarm = (int)MathF.Round(MaxAlarm),
                TimeUsedSeconds = (int)MathF.Round(Duration - TimeLeft),
                Loot = ShadowCarrying && Outcome == HeistOutcome.Success,
                SafeMistakes = SafeMistakes,
            };
        }
    }
}
