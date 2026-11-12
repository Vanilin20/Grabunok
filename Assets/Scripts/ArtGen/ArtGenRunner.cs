using System;
using System.Collections.Generic;
using System.IO;
using Banda.Heist;

namespace Banda.ArtGen
{
    /// <summary>
    /// Точка входу генераторів арту справи. Викликається з меню Unity (Banda → 3. Згенерувати арт справи)
    /// або з консолі (tools/artgen). Пише PNG в Assets/Art/Heist і превʼю в Captures/previews.
    /// </summary>
    public static class ArtGenRunner
    {
        public static List<string> GenerateAll(string projectRoot)
        {
            List<string> written = new List<string>();
            string art = Path.Combine(projectRoot, "Assets", "Art", "Heist");
            HeistLevel level = HeistLevel.CornerStore();

            Save(CharacterArt.Shadow(), art, "shadow_sheet.png", written);
            Save(CharacterArt.Guard(), art, "guard_sheet.png", written);
            Save(GuardIcons.Suspect(), art, "guard_suspect.png", written);
            Save(GuardIcons.Alarm(), art, "guard_alarm.png", written);
            Save(PropArt.Door(false), art, "door_closed.png", written);
            Save(PropArt.Door(true), art, "door_open.png", written);
            Save(PropArt.Safe(false), art, "safe_closed.png", written);
            Save(PropArt.Safe(true), art, "safe_open.png", written);
            Save(PropArt.Camera(), art, "camera_dome.png", written);
            Save(PropArt.Dot(P.Alarm), art, "led_red.png", written);
            foreach (KeyValuePair<string, PixelCanvas> pair in HudArt.All())
            {
                Save(pair.Value, art, pair.Key + ".png", written);
            }

            foreach (KeyValuePair<string, PixelCanvas> pair in HackerArt.All())
            {
                Save(pair.Value, art, pair.Key + ".png", written);
            }

            foreach (KeyValuePair<string, PixelCanvas> pair in SafeArt.All())
            {
                Save(pair.Value, art, pair.Key + ".png", written);
            }

            foreach (KeyValuePair<string, PixelCanvas> pair in NewspaperArt.All(projectRoot))
            {
                Save(pair.Value, art, pair.Key + ".png", written);
            }

            written.AddRange(AudioArt.GenerateAll(projectRoot));

            return written;
        }

        private static void Save(PixelCanvas canvas, string dir, string file, List<string> written)
        {
            string path = Path.Combine(dir, file);
            canvas.Save(path);
            written.Add(path);
        }
    }
}
