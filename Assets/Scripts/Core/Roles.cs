using System.Collections.Generic;
using UnityEngine;

namespace Banda.Core
{
    /// <summary>Ідентифікатори ролей банди (docs/TZ.md, розділ «Ролі»).</summary>
    public enum RoleId
    {
        None = -1,
        Hacker = 0,
        Shadow = 1,
        Safecracker = 2,
        Distractor = 3,
        Driver = 4,
    }

    /// <summary>Дані ролей для інтерфейсу: назва, підказка, колір, етап розробки, з якого роль грабельна.</summary>
    public static class Roles
    {
        private readonly struct Info
        {
            public readonly string Name;
            public readonly string Hint;
            public readonly Color Color;
            public readonly int Stage;

            public Info(string name, string hint, string colorHex, int stage)
            {
                Name = name;
                Hint = hint;
                Color = Palette.Hex(colorHex);
                Stage = stage;
            }
        }

        /// <summary>Поточний етап розробки — ролі з пізніших етапів показуються заблокованими.</summary>
        public const int CurrentStage = 1;

        public static readonly RoleId[] All =
        {
            RoleId.Hacker, RoleId.Shadow, RoleId.Safecracker, RoleId.Distractor, RoleId.Driver,
        };

        private static readonly Dictionary<RoleId, Info> Table = new Dictionary<RoleId, Info>
        {
            { RoleId.Hacker, new Info("Хакер", "Камери, двері, охорона", "3de0ff", 1) },
            { RoleId.Shadow, new Info("Тінь", "Крадеться й бере здобич", "a77bff", 1) },
            { RoleId.Safecracker, new Info("Зломщик", "Відкриває сейф", "f2c14e", 1) },
            { RoleId.Distractor, new Info("Відволікач", "Заговорює охорону", "ff3d7f", 3) },
            { RoleId.Driver, new Info("Водій", "Везе банду під час втечі", "5cf08a", 4) },
        };

        public static bool IsValid(RoleId role) => Table.ContainsKey(role);

        public static bool IsAvailable(RoleId role) => Table.TryGetValue(role, out Info info) && info.Stage <= CurrentStage;

        public static string DisplayName(RoleId role) => Table.TryGetValue(role, out Info info) ? info.Name : "—";

        public static string Hint(RoleId role) => Table.TryGetValue(role, out Info info) ? info.Hint : string.Empty;

        public static Color ColorOf(RoleId role) => Table.TryGetValue(role, out Info info) ? info.Color : Palette.Hex("736e8c");

        public static int UnlockStage(RoleId role) => Table.TryGetValue(role, out Info info) ? info.Stage : 0;
    }
}
