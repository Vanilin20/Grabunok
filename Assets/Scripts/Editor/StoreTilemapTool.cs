using System.Collections.Generic;
using System.IO;
using Banda.ArtGen;
using Banda.Core;
using Banda.Heist;
using Banda.HeistRuntime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Banda.EditorTools
{
    /// <summary>
    /// Інструмент рівня «Магазин на розі» (лише редактор):
    ///  • Banda → 4. Згенерувати тайли магазину — пише PNG тайлів (далі джерело правди — PNG) і Tile-ассети;
    ///  • Banda → 5. Заповнити Tilemap магазину — шари Tilemap зі сітки HeistLevel.StoreGrid + двері, сейф, камери,
    ///    записки й світло з HeistLevel. Ручні правки решти сцени не чіпає; перезаписує лише обʼєкт StoreTilemap
    ///    і згенеровані з рівня обʼєкти під HeistWorld/Level/FromLevel.
    /// </summary>
    public static class StoreTilemapTool
    {
        private const string TileFolder = "Assets/Art/Heist/Tiles";
        private const string TileAssetFolder = "Assets/Art/Heist/Tiles/Assets";
        private const int Pad = 3; // поле довкола рівня в тайлах (дахи, провулок)
        private const int Street = 4; // тротуар і дорога знизу
        private const float T = HeistLevel.Tile;

        [MenuItem("Banda/4. Згенерувати тайли магазину", priority = 4)]
        public static void GenerateTiles()
        {
            Directory.CreateDirectory(TileFolder);
            Directory.CreateDirectory(TileAssetFolder);
            foreach (KeyValuePair<string, PixelCanvas> pair in TileArt.All())
            {
                pair.Value.Save($"{TileFolder}/{pair.Key}.png");
            }

            AssetDatabase.Refresh();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TileFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1f; // 1 px = 1 одиниця, як увесь світ справи; клітинка Grid = 16
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                string name = Path.GetFileNameWithoutExtension(path);
                string assetPath = $"{TileAssetFolder}/{name}.asset";
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, assetPath);
                }

                tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Banda] Тайли магазину згенеровано.");
        }

        private static Tile Get(string name)
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>($"{TileAssetFolder}/{name}.asset");
            if (tile == null)
            {
                Debug.LogError($"[Banda] Немає тайла {name} — спершу Banda → 4. Згенерувати тайли магазину");
            }

            return tile;
        }

        [MenuItem("Banda/5. Заповнити Tilemap магазину", priority = 5)]
        public static void Fill()
        {
            HeistWorldView view = Object.FindFirstObjectByType<HeistWorldView>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogError("[Banda] У сцені немає HeistWorldView");
                return;
            }

            Transform level = view.transform.Find("Level");
            Undo.RegisterFullObjectHierarchyUndo(level.gameObject, "Заповнити магазин");
            DestroyChild(level, "StoreTilemap");
            DestroyChild(level, "FromLevel");
            DestroyChild(level, "StoreBase"); // старий арт рівня одним PNG
            DestroyChild(level, "StoreGlow");

            HeistLevel data = HeistLevel.CornerStore();
            string[] grid = HeistLevel.StoreGrid;
            GameObject gridGo = new GameObject("StoreTilemap", typeof(Grid));
            gridGo.transform.SetParent(level, false);
            gridGo.GetComponent<Grid>().cellSize = new Vector3(T, T, 0f);

            Material lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            Material additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Additive.mat");
            Tilemap outside = Layer(gridGo, "Outside", -4, lit);
            Tilemap floor = Layer(gridGo, "Floor", -3, lit);
            Tilemap shade = Layer(gridGo, "Shade", -2, lit);
            Tilemap decor = Layer(gridGo, "Decor", -1, lit);
            Tilemap walls = Layer(gridGo, "Walls", 1, lit);
            Tilemap props = Layer(gridGo, "Props", 1, lit);
            Tilemap glow = Layer(gridGo, "Glow", 2, additive);
            glow.color = new Color(1f, 1f, 1f, 0.6f); // Legacy Additive подвоює — пів сили

            int w = grid[0].Length;
            int h = grid.Length;
            FillOutside(outside, glow, w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    char c = grid[y][x];
                    Vector3Int cell = Cell(x, y);
                    if (!IsWall(c))
                    {
                        floor.SetTile(cell, x > 17 ? Get("concrete_" + Variant(x, y, 4)) : Get("floor_" + Variant(x, y, 4)));
                        string shadeTile = ShadeFor(grid, x, y);
                        if (shadeTile != null)
                        {
                            shade.SetTile(cell, Get(shadeTile));
                        }
                    }

                    PlaceTile(grid, x, y, c, cell, walls, props, glow, decor);
                }
            }

            // Килимок «ВХІД» перед скляними дверима і трап у підсобці.
            Box front = data.Exits[0];
            for (int i = 0; i < 3; i++)
            {
                decor.SetTile(Cell((int)(front.X / T) + i, (int)(front.Y / T)), Get("mat_" + i));
            }

            decor.SetTile(Cell(21, 7), Get("drain"));
            ScatterLitter(grid, decor);
            PlaceFromLevel(level, view, data);
            SetCameraBounds(view, w, h);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            Debug.Log("[Banda] Tilemap магазину заповнено.");
        }

        private static Tilemap Layer(GameObject grid, string name, int order, Material material)
        {
            GameObject go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid.transform, false);
            TilemapRenderer renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            renderer.sharedMaterial = material;
            return go.GetComponent<Tilemap>();
        }

        /// <summary>Клітинка рівня (x вправо, y униз) → клітинка Tilemap (y угору).</summary>
        private static Vector3Int Cell(int x, int y) => new Vector3Int(x, -y - 1, 0);

        private static int Variant(int x, int y, int count) => (int)(PixelCanvas.Hash(x, y, 404) * count) % count;

        private static bool IsWall(char c) => c == '#' || c == '=' || c == 'D' || c == 'b';

        private static bool IsSolid(char c) => IsWall(c) || "FABEKXmS".IndexOf(c) >= 0;

        private static char At(string[] grid, int x, int y) =>
            y < 0 || y >= grid.Length || x < 0 || x >= grid[0].Length ? '#' : grid[y][x];

        private static string ShadeFor(string[] grid, int x, int y)
        {
            bool n = IsSolid(At(grid, x, y - 1));
            bool wst = IsSolid(At(grid, x - 1, y));
            return n && wst ? "shade_nw" : n ? "shade_n" : wst ? "shade_w" : null;
        }

        private static void PlaceTile(string[] grid, int x, int y, char c, Vector3Int cell, Tilemap walls, Tilemap props, Tilemap glow, Tilemap decor)
        {
            switch (c)
            {
                case '#':
                    int mask = (IsWall(At(grid, x, y - 1)) ? 0 : 1) | (IsWall(At(grid, x + 1, y)) ? 0 : 2)
                               | (IsWall(At(grid, x, y + 1)) ? 0 : 4) | (IsWall(At(grid, x - 1, y)) ? 0 : 8);
                    // Зовнішній бік стіни (поза рівнем) — не підлога; кант лише в бік приміщення.
                    mask &= InsideMask(grid, x, y);
                    walls.SetTile(cell, Get("wall_" + mask));
                    break;
                case '=':
                    walls.SetTile(cell, Get("window_" + (x % 3 == 0 ? 1 : 0)));
                    glow.SetTile(cell, Get("window_glow_" + (x % 2)));
                    break;
                case 'D':
                    int part = At(grid, x - 1, y) != 'D' ? 0 : At(grid, x + 1, y) != 'D' ? 2 : 1;
                    walls.SetTile(cell, Get("door_front_" + part));
                    break;
                case 'b':
                    walls.SetTile(cell, Get("door_back_" + (At(grid, x, y - 1) != 'b' ? 0 : 1)));
                    break;
                case 'F':
                    props.SetTile(cell, Get("fridge_" + Variant(x, y, 3)));
                    glow.SetTile(cell, Get("fridge_glow"));
                    break;
                case 'A':
                case 'B':
                    bool left = At(grid, x - 1, y) != c;
                    bool right = At(grid, x + 1, y) != c;
                    props.SetTile(cell, Get(left ? "shelf_end_l" : right ? "shelf_end_r" : "shelf_" + Variant(x, y, 3)));
                    break;
                case 'E':
                    props.SetTile(cell, Get("promo_" + Variant(x, y, 3)));
                    break;
                case 'K':
                    bool below = At(grid, x, y + 1) == 'K';
                    bool above = At(grid, x, y - 1) == 'K';
                    string counter = above ? "counter_s" : below ? "counter_ne" : At(grid, x - 1, y) != 'K' ? "counter_w" : "counter_register";
                    props.SetTile(cell, Get(counter));
                    if (counter == "counter_register")
                    {
                        glow.SetTile(cell, Get("register_glow"));
                    }

                    break;
                case 'X':
                    props.SetTile(cell, Get("box_" + Variant(x, y, 3)));
                    break;
                case 'm':
                    props.SetTile(cell, Get("desk"));
                    glow.SetTile(cell, Get("desk_glow"));
                    break;
            }
        }

        /// <summary>Біти сторін стіни, що дивляться всередину будівлі (кант світла лише туди).</summary>
        private static int InsideMask(string[] grid, int x, int y)
        {
            int w = grid[0].Length;
            int h = grid.Length;
            int m = 0;
            if (y > 0)
            {
                m |= 1;
            }

            if (x < w - 1)
            {
                m |= 2;
            }

            if (y < h - 1)
            {
                m |= 4;
            }

            if (x > 0)
            {
                m |= 8;
            }

            return m;
        }

        private static void FillOutside(Tilemap outside, Tilemap glow, int w, int h)
        {
            for (int y = -Pad; y < h + Street; y++)
            {
                for (int x = -Pad; x < w + Pad; x++)
                {
                    bool inside = x >= 0 && x < w && y >= 0 && y < h;
                    if (inside)
                    {
                        continue;
                    }

                    Vector3Int cell = Cell(x, y);
                    if (y >= h)
                    {
                        int row = y - h;
                        float wet = PixelCanvas.Hash(x, y, 515);
                        string tile = row < 2 ? "sidewalk_" + (wet < 0.14f ? 2 : wet < 0.3f ? 3 : wet < 0.65f ? 0 : 1) : row == 2 ? "curb" : "asphalt_" + ((x % 4 + 4) % 4 == 0 ? 1 : 0);
                        outside.SetTile(cell, Get(tile));
                        if (row < 2 && tile == "sidewalk_2")
                        {
                            glow.SetTile(cell, Get("puddle_glow_" + ((x % 2 + 2) % 2))); // неон у калюжах — лише під вітринами
                        }
                    }
                    else if (x >= w)
                    {
                        outside.SetTile(cell, Get("asphalt_" + Variant(x, y, 1))); // провулок за задніми дверима
                    }
                    else
                    {
                        outside.SetTile(cell, Get("roof_" + Variant(x, y, 3)));
                    }
                }
            }
        }

        private static void ScatterLitter(string[] grid, Tilemap decor)
        {
            for (int y = 0; y < grid.Length; y++)
            {
                for (int x = 0; x < grid[0].Length; x++)
                {
                    if (grid[y][x] != '.' || decor.HasTile(Cell(x, y)))
                    {
                        continue;
                    }

                    float n = PixelCanvas.Hash(x, y, 909);
                    if (n < 0.05f)
                    {
                        decor.SetTile(Cell(x, y), Get("litter_" + Variant(x, y, 3)));
                    }
                    else if (n > 0.965f)
                    {
                        decor.SetTile(Cell(x, y), Get("stain"));
                    }
                }
            }
        }

        /// <summary>Двері L, сейф, камери, записки й світло — з HeistLevel, щоб гра й картинка не розходились.</summary>
        private static void PlaceFromLevel(Transform level, HeistWorldView view, HeistLevel data)
        {
            Transform from = new GameObject("FromLevel").transform;
            from.SetParent(level, false);
            SerializedObject so = new SerializedObject(view);
            SpriteRenderer door = (SpriteRenderer)so.FindProperty("_door").objectReferenceValue;
            SpriteRenderer safe = (SpriteRenderer)so.FindProperty("_safe").objectReferenceValue;
            door.transform.localPosition = HeistWorldView.ToWorld(new Vec2(data.Door.X, data.Door.Y));
            safe.transform.localPosition = HeistWorldView.ToWorld(data.SafePos);
            // Старі камери й світло, розставлені для попереднього рівня, — прибрати (тепер вони в FromLevel).
            for (int i = level.childCount - 1; i >= 0; i--)
            {
                Transform child = level.GetChild(i);
                if (child.name == "CameraDome" || child.name == "CameraLed" || child.name.StartsWith("Light_"))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            Material lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            Material unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            for (int i = 0; i < data.Cameras.Count; i++)
            {
                Vector3 p = HeistWorldView.ToWorld(data.Cameras[i]);
                Sprite(from, "Camera" + (i + 1), "Assets/Art/Heist/camera_dome.png", p, 4, lit);
                SpriteRenderer led = Sprite(from, "Camera" + (i + 1) + "Led", "Assets/Art/Heist/led_red.png", p + new Vector3(2f, 2f, 0f), 5, unlit);
                led.gameObject.AddComponent<LedBlink>();
            }

            for (int i = 0; i < data.Notes.Count; i++)
            {
                Sprite(from, "Note" + (i + 1), $"{TileFolder}/note.png", HeistWorldView.ToWorld(data.Notes[i]), 2, lit);
            }

            foreach (LightSpot spot in data.Lights)
            {
                GameObject go = new GameObject("Light_" + spot.Name);
                go.transform.SetParent(from, false);
                go.transform.localPosition = HeistWorldView.ToWorld(spot.Pos);
                Light2D light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = Palette.Hex(spot.Color);
                light.intensity = spot.Intensity * 1.6f;
                light.pointLightOuterRadius = spot.Radius;
                light.pointLightInnerRadius = 0f;
                light.falloffIntensity = 0.5f;
                light.targetSortingLayers = AllSortingLayers(); // без цього новий Light2D нічого не освітлює
                if (spot.Flicker != LightFlicker.None)
                {
                    LevelLightFlicker flicker = go.AddComponent<LevelLightFlicker>();
                    SerializedObject f = new SerializedObject(flicker);
                    f.FindProperty("_kind").enumValueIndex = (int)spot.Flicker;
                    f.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static int[] AllSortingLayers()
        {
            SortingLayer[] layers = SortingLayer.layers;
            int[] ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                ids[i] = layers[i].id;
            }

            return ids;
        }

        private static SpriteRenderer Sprite(Transform parent, string name, string asset, Vector3 pos, int order, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset);
            r.sharedMaterial = material;
            r.sortingOrder = order;
            return r;
        }

        private static void SetCameraBounds(HeistWorldView view, int w, int h)
        {
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("_artMin").vector2Value = new Vector2(-Pad * T, -(h + Street) * T);
            so.FindProperty("_artMax").vector2Value = new Vector2((w + Pad) * T, Pad * T);
            so.ApplyModifiedProperties();
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }
}
