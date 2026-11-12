using System;

namespace Banda.Heist
{
    public enum HeistActionKind
    {
        /// <summary>Хакер: вимкнути камеру Index на кілька секунд.</summary>
        CameraOff,
        /// <summary>Хакер: відчинити/зачинити двері підсобки.</summary>
        DoorToggle,
        /// <summary>Тінь: напрям руху (джойстик), Direction довжиною ≤ 1.</summary>
        Move,
        /// <summary>Тінь: сховатися біля укриття / вийти зі схованки.</summary>
        Hide,
        /// <summary>Тінь: взяти здобич з відчиненого сейфа.</summary>
        Take,
        /// <summary>Зломщик: спроба комбінації Numbers.</summary>
        SafeTry,
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
        public static HeistAction SafeTry(params int[] numbers) => new HeistAction { Kind = HeistActionKind.SafeTry, Numbers = numbers ?? Array.Empty<int>() };
    }
}
