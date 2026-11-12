using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Banda.EditorTools
{
    /// <summary>
    /// Налаштування імпорту арту, шрифту й сортувальних шарів під піксель-арт.
    /// Запускається з меню Banda → 1. Налаштувати ассети (ідемпотентно: можна запускати повторно).
    /// </summary>
    public static class BandaAssetSetup
    {
        public const string MenuArt = "Assets/Art/Menu";
        public const string FxArt = "Assets/Art/Fx";
        public const string UiArt = "Assets/Art/UI";
        public const string FontTtf = "Assets/Art/Fonts/press_start_2p.ttf";
        public const string FontAssetPath = "Assets/Art/Fonts/press_start_2p SDF.asset";
        public const string RadialPath = "Assets/Art/Fx/radial.png";

        public static readonly string[] SortingLayers = { "Gang", "Front" };

        /// <summary>Усі символи, що трапляються в інтерфейсі, — вшиваються в атлас наперед.</summary>
        private const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгґдеєжзиіїйклмнопрстуфхцчшщьюя’ʼ«»—–…·★№";

        [MenuItem("Banda/1. Налаштувати ассети", priority = 1)]
        public static void RunAll()
        {
            ConfigureWorldSprites(MenuArt);
            ConfigureWorldSprites(FxArt);
            ConfigureUiSprites();
            EnsureSortingLayers();
            EnsureFontAsset();
            AssetDatabase.SaveAssets();
            Debug.Log("[Banda] Ассети налаштовано.");
        }

        /// <summary>Світові спрайти: 1 піксель = 1 одиниця, Nearest, без стиснення, опора — верхній лівий кут.</summary>
        private static void ConfigureWorldSprites(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            var normalMaps = new Dictionary<string, string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).EndsWith("_n"))
                {
                    normalMaps[path.Replace("_n.png", ".png")] = path;
                }
            }

            // Спершу карти нормалей: на момент налаштування основних текстур вони вже імпортовані як треба.
            foreach (string normalPath in normalMaps.Values)
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (normalMaps.ContainsValue(path))
                {
                    continue;
                }

                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                bool centered = path == RadialPath;
                settings.spriteAlignment = centered ? (int)SpriteAlignment.Center : (int)SpriteAlignment.TopLeft;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteExtrude = 0;
                importer.SetTextureSettings(settings);

                if (normalMaps.TryGetValue(path, out string normalPath))
                {
                    Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                    importer.secondarySpriteTextures = new[]
                    {
                        new SecondarySpriteTexture { name = "_NormalMap", texture = normal },
                    };
                }

                importer.SaveAndReimport();
            }
        }

        /// <summary>UI: 9-slice з полем 8 px, 100 px на одиницю (як reference PPU у CanvasScaler).</summary>
        private static void ConfigureUiSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { UiArt }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.spriteBorder = new Vector4(8f, 8f, 8f, 8f);
                importer.SaveAndReimport();
            }
        }

        private static void EnsureSortingLayers()
        {
            Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject so = new SerializedObject(tagManager);
            SerializedProperty layers = so.FindProperty("m_SortingLayers");
            foreach (string layerName in SortingLayers)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                {
                    if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == layerName)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    continue;
                }

                layers.InsertArrayElementAtIndex(layers.arraySize);
                SerializedProperty entry = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                entry.FindPropertyRelative("name").stringValue = layerName;
                entry.FindPropertyRelative("uniqueID").longValue = (uint)Random.Range(1, int.MaxValue);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Піксельний шрифт для TextMeshPro: растр без згладжування (як у Godot: antialiasing NONE),
        /// розмір вибірки 8 — рідний розмір Press Start 2P, атлас з фільтром Point.
        /// </summary>
        public static TMP_FontAsset EnsureFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontTtf);
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 8, 2, GlyphRenderMode.RASTER, 512, 512, AtlasPopulationMode.Dynamic, false);
            asset.name = "press_start_2p SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);
            Texture2D atlas = asset.atlasTexture;
            atlas.name = "press_start_2p Atlas";
            atlas.filterMode = FilterMode.Point;
            AssetDatabase.AddObjectToAsset(atlas, asset);
            asset.material.name = "press_start_2p Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters(Charset, out string missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning($"[Banda] У шрифті Press Start 2P немає символів: {missing}");
            }

            atlas.filterMode = FilterMode.Point;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            // Робимо шрифт типовим для TextMeshPro — нові тексти одразу піксельні.
            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                SerializedObject so = new SerializedObject(settings);
                SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
                if (prop != null)
                {
                    prop.objectReferenceValue = asset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return asset;
        }
    }
}
