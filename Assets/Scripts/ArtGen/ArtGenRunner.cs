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

            (PixelCanvas store, PixelCanvas glow) = StoreArt.Build(level);
            Save(store, art, "store_base.png", written);
            Save(glow, art, "store_glow.png", written);
            Save(CharacterArt.Shadow(), art, "shadow_sheet.png", written);
            Save(CharacterArt.Guard(), art, "guard_sheet.png", written);
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

        /// <summary>Превʼю екранів поза Unity — для циклу якості, поки редактор недоступний.</summary>
        public static List<string> Previews(string projectRoot, string version)
        {
            List<string> written = new List<string>();
            string dir = Path.Combine(projectRoot, "Captures", "previews");
            foreach (KeyValuePair<string, PixelCanvas> pair in ScreenPreviews.All(projectRoot))
            {
                string name = $"{pair.Key}_{version}";
                Save(pair.Value, dir, name + ".png", written);
                Save(LightPreview.Half(pair.Value), dir, name + "_phone_half.png", written);
            }

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
