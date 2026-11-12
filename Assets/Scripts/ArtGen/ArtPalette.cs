namespace Banda.ArtGen
{
    /// <summary>
    /// Палітра гри — ті самі 32 кольори, що й docs/art/palette.gpl та Banda.Core.Palette.
    /// Генератори арту беруть кольори ТІЛЬКИ звідси (стандарт: одна палітра).
    /// </summary>
    public static class P
    {
        public static readonly Rgba Night0 = Rgba.Hex("07060d");
        public static readonly Rgba Night1 = Rgba.Hex("0e0c1a");
        public static readonly Rgba Night2 = Rgba.Hex("171428");
        public static readonly Rgba Night3 = Rgba.Hex("221d38");
        public static readonly Rgba Night4 = Rgba.Hex("2f2849");
        public static readonly Rgba Night5 = Rgba.Hex("40375e");
        public static readonly Rgba Night6 = Rgba.Hex("574b78");
        public static readonly Rgba Night7 = Rgba.Hex("786b98");
        public static readonly Rgba Brick0 = Rgba.Hex("2a1717");
        public static readonly Rgba Brick1 = Rgba.Hex("45221f");
        public static readonly Rgba Brick2 = Rgba.Hex("63302a");
        public static readonly Rgba Brick3 = Rgba.Hex("874539");
        public static readonly Rgba Brick4 = Rgba.Hex("b0634a");
        public static readonly Rgba Steel0 = Rgba.Hex("3a3f4f");
        public static readonly Rgba Steel1 = Rgba.Hex("5a6175");
        public static readonly Rgba Steel2 = Rgba.Hex("8b93a8");
        public static readonly Rgba Steel3 = Rgba.Hex("c7cede");
        public static readonly Rgba Lamp0 = Rgba.Hex("4a2e1a");
        public static readonly Rgba Lamp1 = Rgba.Hex("8a5a24");
        public static readonly Rgba Lamp2 = Rgba.Hex("d8962f");
        public static readonly Rgba Lamp3 = Rgba.Hex("f7c95a");
        public static readonly Rgba Lamp4 = Rgba.Hex("fff2c4");
        public static readonly Rgba Neon0 = Rgba.Hex("5c1036");
        public static readonly Rgba Neon1 = Rgba.Hex("a3195b");
        public static readonly Rgba Neon2 = Rgba.Hex("ff3d7f");
        public static readonly Rgba Neon3 = Rgba.Hex("ffb3cf");
        public static readonly Rgba Cyan0 = Rgba.Hex("0f3a4d");
        public static readonly Rgba Cyan1 = Rgba.Hex("1a7f99");
        public static readonly Rgba Cyan2 = Rgba.Hex("3de0ff");
        public static readonly Rgba Cyan3 = Rgba.Hex("c2f6ff");
        public static readonly Rgba Alarm = Rgba.Hex("e8433a");
        public static readonly Rgba Ok = Rgba.Hex("5cf08a");

        public static readonly Rgba[] All =
        {
            Night0, Night1, Night2, Night3, Night4, Night5, Night6, Night7,
            Brick0, Brick1, Brick2, Brick3, Brick4, Steel0, Steel1, Steel2, Steel3,
            Lamp0, Lamp1, Lamp2, Lamp3, Lamp4, Neon0, Neon1, Neon2, Neon3,
            Cyan0, Cyan1, Cyan2, Cyan3, Alarm, Ok,
        };

        /// <summary>Найближчий колір палітри (для перевірки й квантування).</summary>
        public static Rgba Nearest(Rgba c)
        {
            Rgba best = All[0];
            int bestD = int.MaxValue;
            foreach (Rgba p in All)
            {
                int dr = p.R - c.R;
                int dg = p.G - c.G;
                int db = p.B - c.B;
                int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }

            return best.WithAlpha(c.A);
        }
    }
}
