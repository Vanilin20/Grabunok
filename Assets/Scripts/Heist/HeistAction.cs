using System;

namespace Banda.Heist
{
    public enum HeistActionKind
    {
        /// <summary>
        /// Хакер: вимкнути камеру Index. Напряму хост цю дію ВІДХИЛЯЄ — камеру вимикає лише розвʼязана головоломка
        /// (HackStart → HackSolve); значення лишено, щоб не зсувати числа в мережевому протоколі.
        /// </summary>
        CameraOff,
        /// <summary>Хакер: відчинити/зачинити двері. Як і CameraOff, напряму відхиляється (двері — HackStart(-1)).</summary>
        DoorToggle,
        /// <summary>Тінь: напрям руху (джойстик), Direction довжиною ≤ 1.</summary>
        Move,
        /// <summary>Тінь: сховатися біля укриття / вийти зі схованки.</summary>
        Hide,
        /// <summary>Тінь: взяти здобич з відчиненого сейфа.</summary>
        Take,
        /// <summary>Зломщик: спроба комбінації Numbers.</summary>
        SafeTry,
        /// <summary>Хакер: узятися за камеру Index (або за двері, Index = -1) — хост видає головоломку з дротами.</summary>
        HackStart,
        /// <summary>Хакер: розвʼязок головоломки. Numbers[i] = номер правого гнізда, куди вставлено дріт i.</summary>
        HackSolve,
        /// <summary>Хакер: кинути головоломку без штрафу.</summary>
        HackCancel,
    }

    /// <summary>
    /// Дія гравця — єдине, що клієнт надсилає хосту (ТЗ: «телефони надсилають лише дії»).
    /// Роль відправника визначає хост за гравцем, а не за полем дії, тож підробити чужу дію не можна.
    /// </summary>
    [Serializable]
    public sealed class HeistAction
    {
        public HeistActionKind Kind;
        public int Index = -1;
        public int[] Numbers = Array.Empty<int>();
        public Vec2 Direction;

        public static HeistAction CameraOff(int index) => new HeistAction { Kind = HeistActionKind.CameraOff, Index = index };
        public static HeistAction DoorToggle() => new HeistAction { Kind = HeistActionKind.DoorToggle };
        public static HeistAction Move(Vec2 direction) => new HeistAction { Kind = HeistActionKind.Move, Direction = direction };
        public static HeistAction Hide() => new HeistAction { Kind = HeistActionKind.Hide };
        public static HeistAction Take() => new HeistAction { Kind = HeistActionKind.Take };
        public static HeistAction HackStart(int index) => new HeistAction { Kind = HeistActionKind.HackStart, Index = index };
        public static HeistAction HackSolve(params int[] sockets) => new HeistAction { Kind = HeistActionKind.HackSolve, Numbers = sockets ?? Array.Empty<int>() };
        public static HeistAction HackCancel() => new HeistAction { Kind = HeistActionKind.HackCancel };
        public static HeistAction SafeTry(params int[] numbers) => new HeistAction { Kind = HeistActionKind.SafeTry, Numbers = numbers ?? Array.Empty<int>() };
    }
}
