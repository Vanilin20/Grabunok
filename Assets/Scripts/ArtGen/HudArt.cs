using System.Collections.Generic;

namespace Banda.ArtGen
{
    /// <summary>
    /// Спільні елементи екранів справи (ТЗ: шкала тривоги вгорі, таймер, стан кожного гравця)
    /// і керування Тіні. Іконки 12×12 читаються за пів секунди: форма + колір, без тексту.
    /// </summary>
    public static class HudArt
    {
        public static Dictionary<string, PixelCanvas> All()
        {
            return new Dictionary<string, PixelCanvas>
            {
                { "hud_bar_frame", BarFrame() },
                { "hud_segment", Solid(5, 6, new Rgba(255, 255, 255)) },
                { "hud_siren", Siren() },
                { "hud_clock", Clock() },
                { "hud_plate", Plate(48, 18) },
                { "hud_chip", Plate(30, 18) },
                { "icon_hacker", IconHacker() },
                { "icon_shadow", IconShadow() },
                { "icon_safecracker", IconSafecracker() },
                { "status_busy", StatusDot(P.Lamp3) },
                { "status_danger", StatusDanger() },
                { "status_offline", StatusOffline() },
                { "stick_base", StickBase() },
                { "stick_knob", StickKnob() },
                { "btn_round", RoundButton(false) },
                { "btn_round_lit", RoundButton(true) },
                { "icon_hide", IconHide() },
                { "icon_take", IconTake() },
            };
        }

        private static PixelCanvas Solid(int w, int h, Rgba c)
        {
            PixelCanvas s = new PixelCanvas(w, h);
            s.Fill(c);
            return s;
        }

        /// <summary>Рамка шкали тривоги 164×12: металева планка з заклепками.</summary>
        private static PixelCanvas BarFrame()
        {
            PixelCanvas f = new PixelCanvas(164, 12);
            f.FillRect(0, 0, 164, 12, P.Night1);
            f.Rect(0, 0, 164, 12, P.Steel0);
            f.HLine(1, 162, 1, P.Steel1);
            f.Set(2, 10, P.Steel2);
            f.Set(161, 10, P.Steel2);
            return f;
        }

        /// <summary>Підкладка 9-slice під таймер/фішки (поле 4 px).</summary>
        private static PixelCanvas Plate(int w, int h)
        {
            PixelCanvas p = new PixelCanvas(w, h);
            p.FillRect(1, 1, w - 2, h - 2, P.Night1.WithAlpha(235));
            p.Rect(0, 0, w, h, P.Night4);
            p.HLine(1, w - 2, 1, P.Night5);
            return p;
        }

        private static PixelCanvas Siren()
        {
            PixelCanvas s = new PixelCanvas(12, 12);
            s.FillRect(2, 9, 8, 2, P.Steel1);
            s.Disc(6, 7, 3.5f, P.Alarm);
            s.FillRect(2, 8, 8, 1, P.Alarm);
            s.Set(5, 5, P.Neon3);
            s.Set(4, 6, P.Neon3);
            s.Line(0, 3, 1, 4, P.Lamp3);
            s.Line(11, 3, 10, 4, P.Lamp3);
            s.Set(6, 0, P.Lamp3);
            s.Set(6, 1, P.Lamp3);
            return s;
        }

        private static PixelCanvas Clock()
        {
            PixelCanvas c = new PixelCanvas(12, 12);
            c.Disc(6, 6, 5, P.Steel3);
            c.Ring(6, 6, 5, P.Night0);
            c.VLine(6, 2, 6, P.Night0);
            c.HLine(6, 8, 6, P.Night0);
            c.Set(6, 1, P.Steel1);
            return c;
        }

        private static PixelCanvas IconHacker()
        {
            PixelCanvas i = new PixelCanvas(12, 12);
            i.FillRect(1, 2, 10, 7, P.Night1);
            i.Rect(1, 2, 10, 7, P.Cyan2);
            i.HLine(3, 6, 4, P.Cyan2);
            i.HLine(3, 8, 6, P.Cyan1);
            i.FillRect(0, 9, 12, 2, P.Steel2);
            return i;
        }

        private static PixelCanvas IconShadow()
        {
            PixelCanvas i = new PixelCanvas(12, 12);
            i.Disc(6, 5, 4, P.Night7);
            i.FillRect(2, 8, 8, 4, P.Night7);
            i.Disc(6, 6, 2.2f, P.Night0);
            i.Set(5, 6, P.Neon3);
            i.Set(7, 6, P.Neon3);
            return i;
        }

        private static PixelCanvas IconSafecracker()
        {
            PixelCanvas i = new PixelCanvas(12, 12);
            i.Disc(6, 6, 5, P.Lamp3);
            i.Disc(6, 6, 3, P.Lamp1);
            i.Ring(6, 6, 5, P.Lamp1);
            i.VLine(6, 1, 3, P.Night0);
            i.Set(6, 6, P.Lamp4);
            return i;
        }

        private static PixelCanvas StatusDot(Rgba c)
        {
            PixelCanvas s = new PixelCanvas(5, 5);
            s.Disc(2, 2, 2, c);
            s.Set(1, 1, P.Lamp4);
            return s;
        }

        private static PixelCanvas StatusDanger()
        {
            PixelCanvas s = new PixelCanvas(5, 7);
            s.FillRect(1, 0, 3, 7, P.Alarm);
            s.Set(2, 5, P.Night0);
            s.FillRect(2, 1, 1, 3, P.Lamp4);
            s.Set(2, 6, P.Lamp4);
            return s;
        }

        private static PixelCanvas StatusOffline()
        {
            PixelCanvas s = new PixelCanvas(5, 5);
            s.Line(0, 0, 4, 4, P.Steel2);
            s.Line(4, 0, 0, 4, P.Steel2);
            return s;
        }

        /// <summary>Основа джойстика 56×56: кільце з рисками напрямків.</summary>
        private static PixelCanvas StickBase()
        {
            PixelCanvas s = new PixelCanvas(56, 56);
            s.Disc(28, 28, 26, P.Night1.WithAlpha(150));
            s.Ring(28, 28, 26, P.Night5);
            s.Ring(28, 28, 25, P.Night3);
            foreach ((int x, int y) in new[] { (28, 5), (28, 51), (5, 28), (51, 28) })
            {
                s.Disc(x, y, 1, P.Night6);
            }

            return s;
        }

        private static PixelCanvas StickKnob()
        {
            PixelCanvas k = new PixelCanvas(22, 22);
            k.Disc(11, 11, 10, P.Night5);
            k.Disc(11, 10, 8, P.Night6);
            k.Ring(11, 11, 10, P.Night7);
            k.Set(8, 6, P.Steel3);
            k.Set(9, 6, P.Steel3);
            return k;
        }

        /// <summary>Кругла кнопка дії 40×40: робоча світиться фіолетовим кантом Тіні, вимкнена — тьмяна.</summary>
        private static PixelCanvas RoundButton(bool lit)
        {
            PixelCanvas b = new PixelCanvas(40, 40);
            b.Disc(20, 20, 18, lit ? P.Night4 : P.Night2);
            b.Ring(20, 20, 18, lit ? P.Night7 : P.Night4);
            if (lit)
            {
                b.Ring(20, 20, 19, P.Night7.WithAlpha(110));
                b.HLine(12, 28, 5, P.Night6);
            }

            return b;
        }

        private static PixelCanvas IconHide()
        {
            // Око, наполовину прикрите полицею: «сховатися».
            PixelCanvas i = new PixelCanvas(16, 16);
            for (int x = 2; x < 14; x++)
            {
                int dy = x < 8 ? (x - 2) / 2 : (13 - x) / 2;
                i.Set(x, 8 - dy, P.Steel3);
                i.Set(x, 8 + dy, P.Steel3);
            }

            i.Disc(8, 8, 2, P.Night7);
            i.FillRect(1, 9, 14, 3, P.Steel1);
            i.HLine(1, 14, 9, P.Steel2);
            return i;
        }

        private static PixelCanvas IconTake()
        {
            // Мішок зі знаком гривні: «взяти здобич».
            PixelCanvas i = new PixelCanvas(16, 16);
            i.Disc(8, 10, 5, P.Lamp1);
            i.FillRect(6, 3, 4, 3, P.Lamp1);
            i.HLine(5, 10, 5, P.Lamp0);
            i.Disc(8, 10, 3, P.Lamp2);
            i.VLine(8, 8, 12, P.Lamp4);
            i.HLine(7, 9, 9, P.Lamp4);
            i.HLine(7, 9, 11, P.Lamp4);
            return i;
        }
    }
}
