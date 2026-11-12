using System;
using System.Collections.Generic;
using Banda.Heist;

namespace Banda.ArtGen
{
    /// <summary>
    /// Превʼю екранів справи 640×360 поза Unity: та сама геометрія, арт і світло, що збирає BandaHeistBuilder.
    /// Потрібне для циклу якості, поки редактор недоступний; текст — растровим шрифтом 5×7 замість TMP.
    /// </summary>
    public static class ScreenPreviews
    {
        public static List<LightPreview.Light> LevelLights(HeistLevel level, float gain)
        {
            List<LightPreview.Light> lights = new List<LightPreview.Light>();
            foreach (LightSpot s in level.Lights)
            {
                lights.Add(new LightPreview.Light
                {
                    X = s.Pos.X + StoreArt.Pad,
                    Y = s.Pos.Y + StoreArt.Pad,
                    Radius = s.Radius,
                    Color = Rgba.Hex(s.Color),
                    Intensity = s.Intensity * gain,
                });
            }

            return lights;
        }

        public static Dictionary<string, PixelCanvas> All(string projectRoot)
        {
            return new Dictionary<string, PixelCanvas>
            {
                { "heist_shadow", ShadowScreen() },
                { "heist_hacker", HackerScreen() },
                { "heist_safe", SafeScreen() },
                { "heist_result", ResultScreen(projectRoot) },
            };
        }

        // --- Тінь -------------------------------------------------------------------------------

        private static PixelCanvas ShadowScreen()
        {
            HeistLevel level = HeistLevel.CornerStore();
            // Сцена превʼю: Тінь крадеться між стелажами, охоронець наближається з ліхтариком.
            Vec2 shadow = new Vec2(10.5f * 16, 6.5f * 16);
            Vec2 guard = new Vec2(12.5f * 16 + 8, 4.4f * 16);
            float guardFacing = MathF.PI * 0.62f;

            (PixelCanvas b, PixelCanvas g) = StoreArt.Build(level);
            PixelCanvas world = new PixelCanvas(b.Width, b.Height);
            world.Blit(b, 0, 0);
            int sx = (int)shadow.X + StoreArt.Pad;
            int sy = (int)shadow.Y + StoreArt.Pad;
            int gx = (int)guard.X + StoreArt.Pad;
            int gy = (int)guard.Y + StoreArt.Pad;
            PixelCanvas safe = PropArt.Safe(false);
            world.Blit(safe, (int)level.SafePos.X + StoreArt.Pad - 10, (int)level.SafePos.Y + StoreArt.Pad - 10);
            world.Blit(PropArt.Door(false), (int)level.Door.X + StoreArt.Pad, (int)level.Door.Y + StoreArt.Pad);
            world.Blit(PropArt.Camera(), (int)level.Cameras[0].X + StoreArt.Pad - 4, (int)level.Cameras[0].Y + StoreArt.Pad - 4);
            PixelCanvas shadowSheet = CharacterArt.Shadow();
            PixelCanvas guardSheet = CharacterArt.Guard();
            world.Blit(shadowSheet.Crop(16 * 3, 16 * 1, 16, 16), sx - 8, sy - 8); // кадр ходьби праворуч
            world.Blit(guardSheet.Crop(16 * 2, 16 * 0, 16, 16), gx - 8, gy - 8); // йде вниз

            List<LightPreview.Light> lights = LevelLights(level, 1.6f);
            lights.Add(new LightPreview.Light { X = gx, Y = gy, Radius = 64, Color = P.Lamp4, Intensity = 1.9f, Direction = guardFacing, HalfAngleDeg = 30 });
            lights.Add(new LightPreview.Light { X = gx, Y = gy, Radius = 14, Color = P.Lamp4, Intensity = 1.2f }); // відсвіт ліхтарика на охоронці
            lights.Add(new LightPreview.Light { X = sx, Y = sy, Radius = 26, Color = P.Night7, Intensity = 2.2f }); // аура Тіні
            PixelCanvas lit = LightPreview.Render(world, g, lights, P.Night6, 0.35f);
            // Кант Тіні не гасне в темряві — гравець завжди бачить себе.
            PixelCanvas frame = shadowSheet.Crop(16 * 3, 16 * 1, 16, 16);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (frame.Get(x, y).Equals(P.Night7))
                    {
                        lit.Set(sx - 8 + x, sy - 8 + y, P.Night7);
                    }
                }
            }

            LightPreview.Bloom(lit, 0.72f, 0.7f, 3);
            // Камера: ×2, центр на Тіні.
            PixelCanvas view = lit.Crop(sx - 160, sy - 90, 320, 180);
            PixelCanvas screen = LightPreview.Scale(view, 2);
            DrawHud(screen, 38f, 462f, RoleIdx.Shadow);
            // Джойстик і кнопки.
            Dictionary<string, PixelCanvas> hud = HudArt.All();
            screen.Blit(hud["stick_base"], 30, 270);
            screen.Blit(hud["stick_knob"], 30 + 28 - 11 + 10, 270 + 28 - 11);
            screen.Blit(hud["btn_round_lit"], 560, 296);
            screen.Blit(hud["icon_hide"], 572, 308);
            screen.Blit(hud["btn_round"], 510, 300);
            PixelCanvas take = hud["icon_take"];
            screen.Blit(take, 522, 312);
            PixelFont.DrawCentered(screen, "СХОВАТИСЬ", 580, 340, P.Night7);
            PixelFont.DrawCentered(screen, "ВЗЯТИ", 530, 344, P.Night5);
            HotSeat(screen, 1);
            return screen;
        }

        private enum RoleIdx
        {
            Hacker = 0,
            Shadow = 1,
            Safe = 2,
        }

        /// <summary>Спільний HUD: шкала тривоги з порогом 50%, таймер, фішки гравців.</summary>
        private static void DrawHud(PixelCanvas s, float alarm, float timeLeft, RoleIdx active)
        {
            Dictionary<string, PixelCanvas> hud = HudArt.All();
            int bx = 320 - 82;
            int by = 6;
            s.FillRect(bx - 22, by - 3, 164 + 70, 20, P.Night0.WithAlpha(170));
            s.Blit(hud["hud_siren"], bx - 18, by);
            s.Blit(hud["hud_bar_frame"], bx, by);
            for (int i = 0; i < 20; i++)
            {
                float t = (i + 1) / 20f;
                bool on = alarm / 100f >= t - 0.025f;
                Rgba c = t <= 0.5f ? P.Lamp3 : t <= 0.8f ? P.Lamp2 : P.Alarm;
                s.FillRect(bx + 3 + i * 8, by + 3, 7, 6, on ? c : P.Night3);
            }

            int mark = bx + 3 + 10 * 8 - 1;
            s.VLine(mark, by - 2, by + 13, P.Lamp4);
            PixelFont.Draw(s, "50%", mark - 7, by + 15, P.Lamp4);
            PixelFont.Draw(s, $"{(int)alarm}%", bx + 170, by + 2, P.Lamp3);

            int minutes = (int)timeLeft / 60;
            int seconds = (int)timeLeft % 60;
            s.Blit(hud["hud_plate"], 586, 4);
            s.Blit(hud["hud_clock"], 590, 7);
            PixelFont.Draw(s, $"{minutes}:{seconds:00}", 604, 9, P.Steel3);

            string[] icons = { "icon_hacker", "icon_shadow", "icon_safecracker" };
            string[] status = { "status_busy", null, "status_danger" };
            for (int i = 0; i < 3; i++)
            {
                int cx = 8 + i * 34;
                s.Blit(hud["hud_chip"], cx, 4);
                s.Blit(hud[icons[i]], cx + 3, 7);
                if (status[i] != null)
                {
                    s.Blit(hud[status[i]], cx + 20, 8);
                }

                if (i == (int)active)
                {
                    s.HLine(cx + 2, cx + 27, 20, P.Night7);
                }
            }
        }

        private static void HotSeat(PixelCanvas s, int active)
        {
            string[] labels = { "1 ХАКЕР", "2 ТІНЬ", "3 СЕЙФ" };
            int x = 8;
            for (int i = 0; i < 3; i++)
            {
                int w = PixelFont.Measure(labels[i]) + 8;
                s.FillRect(x, 26, w, 12, (i == active ? P.Night5 : P.Night1).WithAlpha(220));
                s.Rect(x, 26, w, 12, i == active ? P.Night7 : P.Night4);
                PixelFont.Draw(s, labels[i], x + 4, 29, i == active ? P.Steel3 : P.Steel1);
                x += w + 3;
            }
        }

        // --- Хакер ------------------------------------------------------------------------------

        private static PixelCanvas HackerScreen()
        {
            HeistLevel level = HeistLevel.CornerStore();
            Dictionary<string, PixelCanvas> art = HackerArt.All();
            PixelCanvas s = new PixelCanvas(640, 360);
            // Вміст екрана ноутбука.
            s.FillRect(HackerArt.ScreenX, HackerArt.ScreenY, HackerArt.ScreenW, HackerArt.ScreenH, P.Night0);
            int ox = HackerArt.ScreenX + HackerArt.SchemeX;
            int oy = HackerArt.ScreenY + HackerArt.SchemeY;
            s.Blit(art["hacker_schematic"], ox, oy);
            PixelFont.Draw(s, "ОХОРОНА.EXE  ·  МАГАЗИН НА РОЗІ", HackerArt.ScreenX + 10, HackerArt.ScreenY + 8, P.Cyan2);
            s.HLine(HackerArt.ScreenX + 8, HackerArt.ScreenX + HackerArt.ScreenW - 9, HackerArt.ScreenY + 18, P.Cyan1);
            // Камера (вимкнена — червона, таймер), двері, охоронець.
            Vec2 cam = level.Cameras[0];
            s.Blit(art["h_cam_off"], ox + (int)cam.X - 6, oy + (int)cam.Y - 5);
            PixelFont.Draw(s, "4С", ox + (int)cam.X + 10, oy + (int)cam.Y - 3, P.Alarm);
            s.Blit(art["h_door_open"], ox + (int)level.Door.X - 2, oy + (int)level.Door.Y + 8);
            Vec2 guard = new Vec2(9 * 16, 3.5f * 16);
            s.Blit(art["h_guard"], ox + (int)guard.X - 4, oy + (int)guard.Y - 4);
            for (int i = 1; i < 30; i++)
            {
                s.Set(ox + (int)guard.X + i, oy + (int)guard.Y + i / 3, P.Alarm.WithAlpha((byte)(200 - i * 6)));
            }

            // Стрічка з камери праворуч.
            int fx = HackerArt.ScreenX + HackerArt.ScreenW - 120;
            int fy = HackerArt.ScreenY + 30;
            s.Blit(art["h_feed_frame"], fx - 2, fy - 11);
            s.Blit(art["h_feed"], fx, fy - 2);
            s.Blit(art["crt_noise_1"], fx, fy - 2);
            PixelFont.Draw(s, "ВИМКНЕНО", fx + 24, fy + 30, P.Alarm);
            // Підказки дій.
            PixelFont.Draw(s, "ТАП ПО КАМЕРІ — ВИМКНУТИ НА 6 С", fx - 70, HackerArt.ScreenY + 228, P.Cyan1);
            PixelFont.Draw(s, "ТАП ПО ДВЕРЯХ — ВІДЧИНИТИ", fx - 70, HackerArt.ScreenY + 240, P.Cyan1);
            // CRT: сканлайни + віньєтка.
            for (int y = HackerArt.ScreenY + 1; y < HackerArt.ScreenY + HackerArt.ScreenH; y += 2)
            {
                s.HLine(HackerArt.ScreenX, HackerArt.ScreenX + HackerArt.ScreenW - 1, y, P.Night0.WithAlpha(70));
            }

            s.Blit(art["crt_vignette"], HackerArt.ScreenX, HackerArt.ScreenY);
            LightPreview.Bloom(s, 0.7f, 0.6f, 2);
            s.Blit(art["laptop_frame"], 0, 0);
            DrawHud(s, 38f, 462f, RoleIdx.Hacker);
            HotSeat(s, 0);
            return s;
        }

        // --- Зломщик ----------------------------------------------------------------------------

        private static PixelCanvas SafeScreen()
        {
            Dictionary<string, PixelCanvas> art = SafeArt.All();
            PixelCanvas s = new PixelCanvas(640, 360);
            s.Blit(art["safe_front"], 0, 0);
            int number = 23;
            PixelCanvas dial = art["safe_dial_sheet"].Crop(number * SafeArt.DialSize, 0, SafeArt.DialSize, SafeArt.DialSize);
            s.Blit(dial, SafeArt.DialCenterX - SafeArt.DialSize / 2, SafeArt.DialCenterY - SafeArt.DialSize / 2);
            s.Blit(art["safe_marker"], SafeArt.DialCenterX - 5, SafeArt.DialCenterY - SafeArt.DialSize / 2 - 10);
            s.Blit(art["safe_lamp_on"], 250 - 30 - 7, 93);
            s.Blit(art["safe_lamp_off"], 250 - 7, 93);
            s.Blit(art["safe_lamp_off"], 250 + 30 - 7, 93);
            s.Blit(art["safe_lcd"], 250 - 25, 271);
            PixelFont.DrawCentered(s, "17 · 23", 250, 276, P.Cyan3);
            // Спалах влучання — на ПК замість вібрації: золотий кант довкола диска.
            s.Ring(SafeArt.DialCenterX, SafeArt.DialCenterY, SafeArt.DialSize / 2 + 12, P.Lamp3);
            s.Ring(SafeArt.DialCenterX, SafeArt.DialCenterY, SafeArt.DialSize / 2 + 13, P.Lamp2);
            // Кнопки «зафіксувати» / «скинути».
            s.FillRect(370, 290, 110, 26, P.Night2);
            s.Rect(370, 290, 110, 26, P.Lamp3);
            PixelFont.DrawCentered(s, "ЗАФІКСУВАТИ", 425, 299, P.Lamp3);
            s.FillRect(490, 290, 84, 26, P.Night2);
            s.Rect(490, 290, 84, 26, P.Steel1);
            PixelFont.DrawCentered(s, "СКИНУТИ", 532, 299, P.Steel2);
            PixelFont.Draw(s, "КРУТИ ДИСК ПАЛЬЦЕМ. ВЛУЧИВ — СПАЛАХ І КЛАЦ.", 80, 340, P.Steel3);
            LightPreview.Bloom(s, 0.82f, 0.5f, 2);
            DrawHud(s, 38f, 462f, RoleIdx.Safe);
            HotSeat(s, 2);
            return s;
        }

        // --- Підсумок ---------------------------------------------------------------------------

        private static PixelCanvas ResultScreen(string projectRoot)
        {
            Dictionary<string, PixelCanvas> art = NewspaperArt.All(projectRoot);
            PixelCanvas s = new PixelCanvas(640, 360);
            s.Blit(art["news_table"], 0, 0);
            int px = 320 - NewspaperArt.PageW / 2;
            int py = 24;
            // Тінь від сторінки.
            s.FillRect(px + 6, py + 6, NewspaperArt.PageW, NewspaperArt.PageH, P.Night0.WithAlpha(150));
            s.Blit(art["news_page"], px, py);
            PixelFont.DrawCentered(s, "БАНДА ВИНЕСЛА СЕЙФ", 320, py + NewspaperArt.HeadlineY + 4, P.Night0, 2);
            PixelFont.DrawCentered(s, "МАГАЗИНУ НА РОЗІ", 320, py + NewspaperArt.HeadlineY + 22, P.Night0, 2);
            int cx = px + 196;
            int cy = py + 108;
            PixelFont.Draw(s, "ЧАС СПРАВИ: 4:12", cx, cy, P.Night1);
            PixelFont.Draw(s, "МАКС. ТРИВОГА: 38%", cx, cy + 12, P.Night1);
            PixelFont.Draw(s, "ПОМИЛОК НА СЕЙФІ: 1", cx, cy + 24, P.Night1);
            PixelFont.Draw(s, "ЗДОБИЧ: Є", cx, cy + 36, P.Night1);
            PixelCanvas stamp = art["news_stamp_win"];
            s.Blit(stamp, px + NewspaperArt.PageW - stamp.Width - 14, py + 196);
            // Кнопки.
            s.FillRect(180, 332, 120, 22, P.Night2);
            s.Rect(180, 332, 120, 22, P.Lamp3);
            PixelFont.DrawCentered(s, "У ЛОБІ", 240, 339, P.Lamp3);
            s.FillRect(340, 332, 120, 22, P.Night2);
            s.Rect(340, 332, 120, 22, P.Cyan2);
            PixelFont.DrawCentered(s, "ВИЙТИ", 400, 339, P.Cyan2);
            return s;
        }
    }
}
