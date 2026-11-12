using System.Collections.Generic;
using Banda.Core;

namespace Banda.Heist
{
    public enum HeistOutcome
    {
        None,
        Success,
        Caught,
        Timeout,
    }

    /// <summary>Стан гравця на спільній панелі (ТЗ: зайнятий, чекає, у небезпеці).</summary>
    public enum PlayerStatus
    {
        Waiting,
        Busy,
        Danger,
    }

    // ------------------------------------------------------------------------------------------
    // Що бачить кожна роль. Кожен клас містить ТІЛЬКИ поля, дозволені цій ролі:
    // захист від підглядання тримається на типах, а не на дисципліні екранів.
    // ------------------------------------------------------------------------------------------

    /// <summary>Спільне для всіх: тривога, таймер, стан банди (ТЗ: «шкала тривоги, таймер, стан кожного гравця»).</summary>
    public sealed class SharedView
    {
        public float Alarm;
        public float TimeLeft;
        public bool Alert;
        public List<PlayerChip> Players = new List<PlayerChip>();
    }

    public sealed class PlayerChip
    {
        public string Name = string.Empty;
        public RoleId Role = RoleId.None;
        public PlayerStatus Status = PlayerStatus.Waiting;
        public bool Connected = true;
    }

    /// <summary>Базовий тип знімків ролі — для транспорту (серіалізація по ролі).</summary>
    public abstract class RoleView
    {
        public abstract RoleId Role { get; }
    }

    public abstract class RoleStatic
    {
        public abstract RoleId Role { get; }
    }

    // --- Хакер: схема поверху, камери, двері, охоронець. Без Тіні й без комбінації. ---

    public sealed class HackerStatic : RoleStatic
    {
        public override RoleId Role => RoleId.Hacker;
        public Vec2 Size;
        public List<Box> Walls = new List<Box>();
        public List<Box> Covers = new List<Box>();
        public Box Door;
        public List<Vec2> Cameras = new List<Vec2>();
        public float CameraRange;
        public float CameraOffTime;
        public Vec2 Safe;
        public List<Box> Exits = new List<Box>();
    }

    public sealed class HackerView : RoleView
    {
        public override RoleId Role => RoleId.Hacker;
        /// <summary>Скільки секунд кожна камера ще вимкнена (0 — працює).</summary>
        public List<float> CamerasOff = new List<float>();
        public bool DoorOpen;
        public Vec2 Guard;
        public float GuardFacing;
        public bool Alert;
    }

    // --- Тінь: лише коло навколо себе. Без камер на схемі, без комбінації. ---

    public sealed class ShadowStatic : RoleStatic
    {
        public override RoleId Role => RoleId.Shadow;
        public float ViewRadius;
    }

    public sealed class ShadowView : RoleView
    {
        public override RoleId Role => RoleId.Shadow;
        public Vec2 Position;
        public bool Hidden;
        public bool Carrying;
        public bool Seen;
        public bool CanHide;
        public bool CanTake;
        public List<Box> Walls = new List<Box>();
        public List<Box> Covers = new List<Box>();
        /// <summary>Камери, що потрапили в коло видимості (Тінь бачить корпус камери, але не стан).</summary>
        public List<Vec2> Cameras = new List<Vec2>();
        public bool GuardVisible;
        public Vec2 Guard;
        public float GuardFacing;
        public bool DoorVisible;
        public Box Door;
        public bool DoorOpen;
        public bool SafeVisible;
        public Vec2 Safe;
        public bool SafeOpen;
        /// <summary>Виходи в колі видимості.</summary>
        public List<Box> Exits = new List<Box>();
    }

    // --- Зломщик: комбінація й стан сейфа. Більше нічого. ---

    public sealed class SafecrackerStatic : RoleStatic
    {
        public override RoleId Role => RoleId.Safecracker;
        public int[] Combo = System.Array.Empty<int>();
        public int Range;
    }

    public sealed class SafecrackerView : RoleView
    {
        public override RoleId Role => RoleId.Safecracker;
        public bool Open;
        public int Mistakes;
    }

    /// <summary>Знімок, який отримує один гравець за тік: спільне + тільки своя роль.</summary>
    public sealed class RoleSnapshot
    {
        public SharedView Shared = new SharedView();
        public RoleView View;
    }

    /// <summary>Підсумок справи для екрана результату.</summary>
    public sealed class HeistSummary
    {
        public HeistOutcome Outcome = HeistOutcome.None;
        public int MaxAlarm;
        public int TimeUsedSeconds;
        public bool Loot;
        public int SafeMistakes;
    }
}
