using System.Collections.Generic;
using System.IO;
using Banda.ArtGen;
using UnityEditor;
using UnityEngine;

namespace Banda.EditorTools
{
    /// <summary>
    /// Генерація арту справи (Banda.ArtGen — чистий C#) і налаштування його імпорту під піксель-арт.
    /// Меню: Banda → 3. Згенерувати арт справи. Те саме робить консоль: tools/artgen.
    /// </summary>
    public static class BandaHeistArt
    {
        public const string Folder = "Assets/Art/Heist";

        /// <summary>Світові спрайти (1 px = 1 одиниця) з опорою в лівому верхньому куті.</summary>
        private static readonly HashSet<string> WorldTopLeft = new HashSet<string>
        {
            "store_base", "store_glow", "door_closed", "door_open",
        };

        /// <summary>Світові спрайти з опорою в центрі.</summary>
        private static readonly HashSet<string> WorldCenter = new HashSet<string>
        {
            "safe_closed", "safe_open", "camera_dome", "led_red", "shadow_sheet", "guard_sheet", "guard_suspect", "guard_alarm",
        };

        /// <summary>UI-спрайти з 9-slice (поле 4 px).</summary>
        private static readonly HashSet<string> Sliced = new HashSet<string> { "hud_plate", "hud_chip" };

        [MenuItem("Banda/3. Згенерувати арт справи", priority = 3)]
        public static void GenerateAndImport()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            List<string> written = ArtGenRunner.GenerateAll(root);
            AssetDatabase.Refresh();
            ConfigureImport();
            Debug.Log($"[Banda] Арт справи: {written.Count} файлів у {Folder}");
        }

        public static void ConfigureImport()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Tiles/"))
                {
                    continue; // тайли рівня імпортує StoreTilemapTool (1 px = 1 одиниця, клітинка 16)
                }

                string name = Path.GetFileNameWithoutExtension(path);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool world = WorldTopLeft.Contains(name) || WorldCenter.Contains(name);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = world ? 1f : 100f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = name == "crt_scan" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.maxTextureSize = 8192; // стрічка диска сейфа — 4480 px завширшки
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = WorldTopLeft.Contains(name) ? (int)SpriteAlignment.TopLeft : (int)SpriteAlignment.Center;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteExtrude = 0;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = Sliced.Contains(name) ? new Vector4(4f, 4f, 4f, 4f) : Vector4.zero;
                importer.SaveAndReimport();
            }
        }
    }
}
