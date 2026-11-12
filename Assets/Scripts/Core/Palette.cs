using UnityEngine;

namespace Banda.Core
{
    /// <summary>
    /// Палітра гри — 32 кольори (перенесено з Godot: scripts/systems/palette.gd).
    /// Руками не доповнювати: нові кольори спершу погоджуються в стайлгайді.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Night0 = Hex("07060d");
        public static readonly Color Night1 = Hex("0e0c1a");
        public static readonly Color Night2 = Hex("171428");
        public static readonly Color Night3 = Hex("221d38");
        public static readonly Color Night4 = Hex("2f2849");
        public static readonly Color Night5 = Hex("40375e");
        public static readonly Color Night6 = Hex("574b78");
        public static readonly Color Night7 = Hex("786b98");
        public static readonly Color Brick0 = Hex("2a1717");
        public static readonly Color Brick1 = Hex("45221f");
        public static readonly Color Brick2 = Hex("63302a");
        public static readonly Color Brick3 = Hex("874539");
        public static readonly Color Brick4 = Hex("b0634a");
        public static readonly Color Steel0 = Hex("3a3f4f");
        public static readonly Color Steel1 = Hex("5a6175");
        public static readonly Color Steel2 = Hex("8b93a8");
        public static readonly Color Steel3 = Hex("c7cede");
        public static readonly Color Lamp0 = Hex("4a2e1a");
        public static readonly Color Lamp1 = Hex("8a5a24");
        public static readonly Color Lamp2 = Hex("d8962f");
        public static readonly Color Lamp3 = Hex("f7c95a");
        public static readonly Color Lamp4 = Hex("fff2c4");
        public static readonly Color Neon0 = Hex("5c1036");
        public static readonly Color Neon1 = Hex("a3195b");
        public static readonly Color Neon2 = Hex("ff3d7f");
        public static readonly Color Neon3 = Hex("ffb3cf");
        public static readonly Color Cyan0 = Hex("0f3a4d");
        public static readonly Color Cyan1 = Hex("1a7f99");
        public static readonly Color Cyan2 = Hex("3de0ff");
        public static readonly Color Cyan3 = Hex("c2f6ff");
        public static readonly Color Alarm = Hex("e8433a");
        public static readonly Color Ok = Hex("5cf08a");

        // Ролі кольорів в інтерфейсі.
        public static readonly Color NightUi = Night0;
        public static readonly Color Deep = Night1;
        public static readonly Color Slate = Night4;
        public static readonly Color Muted = Steel2; // другорядний текст: контраст 6:1 на Night1
        public static readonly Color Text = Steel3;
        public static readonly Color Lamp = Lamp3;
        public static readonly Color Neon = Neon2;
        public static readonly Color Cyan = Cyan2;
        public static readonly Color Danger = Alarm;
        public static readonly Color Safe = Ok;

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.magenta;
        }
    }
}
