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
        /// <summary>Множник інтенсивності світла рівня (підібрано по рендеру з Unity при нічному світлі 0.22).</summary>
        private const float LightGain = 2.6f;

        [MenuItem("Banda/4. Згенерувати тайли магазину", priority = 4)]
        public static void GenerateTiles()
        {
            Directory.CreateDirectory(TileFolder);
            Directory.CreateDirectory(TileAssetFolder);
            foreach (KeyValuePair<string, PixelCanvas> pair in TileArt.All())
            {
                pair.Value.Save($"{TileFolder}/{pair.Key}.png");
            }

            // Меблі 3/4 (стелажі, холодильники, промо-стіл) — унікальні тайли за клітинками сітки.
            foreach (KeyValuePair<string, PixelCanvas> pair in StoreFurnitureArt.ForGrid(HeistLevel.StoreGrid))
            {
                pair.Value.Save($"{TileFolder}/{pair.Key}.png");
            }

            // Окремі спрайти рівня: вивіска на фасаді й стельові світильники (джерела світла, яке видно).
            StoreFurnitureArt.Sign(false).Save($"{TileFolder}/store_sign.png");
            StoreFurnitureArt.Sign(true).Save($"{TileFolder}/store_sign_glow.png");
            StoreFurnitureArt.CeilingLamp(false).Save($"{TileFolder}/ceiling_lamp.png");
            StoreFurnitureArt.CeilingLamp(true).Save($"{TileFolder}/ceiling_lamp_glow.png");
            StoreFurnitureArt.StreetLamp(false).Save($"{TileFolder}/street_lamp.png");
            StoreFurnitureArt.StreetLamp(true).Save($"{TileFolder}/street_lamp_glow.png");
            StoreFurnitureArt.Bollard().Save($"{TileFolder}/bollard.png");

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
            // Розставлені дрібниці: зміст у порожніх зонах підлоги (без колізії — дрібні предмети, крізь них проходять).
            (int X, int Y, int Kind)[] floorProps =
            {
                (5, 13, 6), (5, 12, 6), (3, 12, 2), (4, 11, 2), (7, 11, 7), (8, 11, 4), (12, 11, 3), (14, 12, 5),
                (12, 3, 3), (15, 6, 5), (12, 2, 0), (10, 12, 1), (23, 10, 3), (21, 12, 8), (19, 6, 5), (23, 5, 0),
            };
            foreach ((int X, int Y, int Kind) p in floorProps)
            {
                if (grid[p.Y][p.X] == '.')
                {
                    decor.SetTile(Cell(p.X, p.Y), Get("litter_" + p.Kind));
                }
            }
            outside.SetTile(Cell(6, h + 1), Get("drain")); // решітки вентиляції на тротуарі — звідти пара
            outside.SetTile(Cell(19, h + 1), Get("drain"));
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

        private static bool IsSolid(char c) => IsWall(c) || "FABCEKXmSPZ".IndexOf(c) >= 0;

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
                case '#' when At(grid, x, y + 1) == 'F':
                    // Верх холодильника, що стоїть під стіною (3/4): малюється замість стіни.
                    props.SetTile(cell, Get($"fridge_{x}_{y}"));
                    glow.SetTile(cell, Get($"fridge_glow_{x}_{y}"));
                    break;
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
                    props.SetTile(cell, Get($"fridge_{x}_{y}"));
                    glow.SetTile(cell, Get($"fridge_glow_{x}_{y}"));
                    break;
                case 'A':
                case 'B':
                case 'C':
                    // Стелаж 3/4 на дві клітинки: тайл верхньої (кришка + верхній ярус) або нижньої (ярус + цоколь).
                    props.SetTile(cell, Get($"rack_{x}_{y}"));
                    break;
                case 'E':
                    props.SetTile(cell, Get($"promo_{x}_{y}"));
                    break;
                case 'P':
                    props.SetTile(cell, Get($"pallet_{x}_{y}"));
                    break;
                case 'Z':
                    props.SetTile(cell, Get($"freezer_{x}_{y}"));
                    glow.SetTile(cell, Get($"freezer_glow_{x}_{y}"));
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
                        // Дах сусідів: здебільшого гравій, зрідка шов з калюжею, витяжка, кондиціонер чи світловий люк.
                        float r = PixelCanvas.Hash(x, y, 616);
                        int roof = r < 0.55f ? 0 : r < 0.7f ? 1 : r < 0.8f ? 2 : r < 0.92f ? 3 : 4;
                        outside.SetTile(cell, Get("roof_" + roof));
                        if (roof == 4)
                        {
                            glow.SetTile(cell, Get("roof_glow_4"));
                        }
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
                    if (n < 0.06f)
                    {
                        decor.SetTile(Cell(x, y), Get("litter_" + Variant(x, y, 6)));
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

            // Тінеутворювачі: стіни й меблі з HeistLevel (ті самі прямокутники, що зупиняють Тінь у симуляції).
            Transform casters = new GameObject("ShadowCasters").transform;
            casters.SetParent(from, false);
            foreach (Box box in data.Walls)
            {
                AddCaster(casters, box);
            }

            foreach (Box box in data.Covers)
            {
                AddCaster(casters, box);
            }

            // Поле камери — слабка червона пляма точно на дальність виявлення симуляції.
            for (int i = 0; i < data.Cameras.Count; i++)
            {
                GameObject go = new GameObject("CameraField" + (i + 1));
                go.transform.SetParent(from, false);
                go.transform.localPosition = HeistWorldView.ToWorld(data.Cameras[i]);
                Light2D field = go.AddComponent<Light2D>();
                field.lightType = Light2D.LightType.Point;
                field.color = Palette.Danger;
                field.intensity = 0.3f; // поле камери — підказка, а не найяскравіша пляма кадру
                field.pointLightOuterRadius = HeistSim.CameraRange;
                field.pointLightInnerRadius = HeistSim.CameraRange * 0.8f;
                field.falloffIntensity = 0.8f;
                field.targetSortingLayers = AllSortingLayers();
            }

            // Вивіска «МАРКЕТ 24» на фасаді під вітринами: саме вона — джерело рожевого й бірюзового неону вулиці.
            Vector3 signPos = HeistWorldView.ToWorld(StoreSignCenter);
            Sprite(from, "StoreSign", $"{TileFolder}/store_sign.png", signPos, 3, lit);
            Sprite(from, "StoreSignGlow", $"{TileFolder}/store_sign_glow.png", signPos, 4,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Additive.mat"));
            // Відблиск вивіски на мокрому тротуарі: віддзеркалена й притушена копія неону під нею.
            SpriteRenderer reflection = Sprite(from, "StoreSignReflection", $"{TileFolder}/store_sign_glow.png", signPos + new Vector3(0f, -15f, 0f), 2,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Additive.mat"));
            reflection.flipY = true;
            reflection.color = new Color(1f, 1f, 1f, 0.14f); // ледь помітно: мокрий тротуар, а не дзеркало
            // Дощ лише надворі: над тротуаром і дорогою, над дахами ліворуч і провулком праворуч.
            int gw = HeistLevel.StoreGrid[0].Length;
            int gh = HeistLevel.StoreGrid.Length;
            AddRain(from, new Vec2(-Pad * T, gh * T), new Vec2((gw + 2 * Pad) * T, Street * T));
            AddRain(from, new Vec2(-Pad * T, -Pad * T), new Vec2(Pad * T, (gh + Pad) * T));
            AddRain(from, new Vec2(gw * T, -Pad * T), new Vec2(Pad * T, (gh + Pad) * T));
            // Передній план вулиці: ліхтарний стовп (джерело теплого світла на тротуарі) і стовпчики біля бордюру.
            Material litMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            Sprite(from, "StreetLamp", $"{TileFolder}/street_lamp.png", HeistWorldView.ToWorld(new Vec2(14.5f * T, (gh + 2.2f) * T - 26f)), 11, litMat);
            Sprite(from, "StreetLampGlow", $"{TileFolder}/street_lamp_glow.png", HeistWorldView.ToWorld(new Vec2(14.5f * T, (gh + 2.2f) * T - 26f)), 12,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Additive.mat"));
            foreach (float bx in new[] { 1.5f, 7.5f, 21.5f })
            {
                Sprite(from, "Bollard", $"{TileFolder}/bollard.png", HeistWorldView.ToWorld(new Vec2(bx * T, (gh + 2.4f) * T - 4f)), 11, litMat);
            }

            AddSteam(from, HeistWorldView.ToWorld(new Vec2(6.5f * T, 16.4f * T)));
            AddSteam(from, HeistWorldView.ToWorld(new Vec2(19.5f * T, 16.6f * T)));

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
                // Світло = геймплей: чітка пляма з ядром (inner) і коротким спадом, між плямами — справжня темрява.
                light.intensity = spot.Intensity * LightGain;
                light.pointLightOuterRadius = spot.Radius * 1.25f;
                light.pointLightInnerRadius = spot.Radius * 0.45f;
                // Лампи — чітка межа «світло/тінь» (стелс читає її миттєво); вікна, неон і екрани — м'який спад.
                light.falloffIntensity = spot.Name.StartsWith("lamp") ? 0.7f : 0.3f;
                light.targetSortingLayers = AllSortingLayers(); // без цього новий Light2D нічого не освітлює
                // Лампи кидають чіткі тіні від стелажів і стін — межа світла читається як геймплей (R5).
                bool lamp = spot.Name.StartsWith("lamp") || spot.Name.StartsWith("backroom");
                light.shadowsEnabled = lamp;
                light.shadowIntensity = 0.8f;
                light.shadowSoftness = 0f;
                if (lamp)
                {
                    AddDust(go.transform, spot.Radius);
                    // Видимий світильник на стелі — передній план, над персонажами.
                    Sprite(go.transform, "Fixture", $"{TileFolder}/ceiling_lamp.png", new Vector3(0f, 10f, 0f), 9, unlit);
                    Sprite(go.transform, "FixtureGlow", $"{TileFolder}/ceiling_lamp_glow.png", new Vector3(0f, 10f, 0f), 10,
                        AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Additive.mat"));
                }
                if (spot.Flicker != LightFlicker.None)
                {
                    LevelLightFlicker flicker = go.AddComponent<LevelLightFlicker>();
                    SerializedObject f = new SerializedObject(flicker);
                    f.FindProperty("_kind").enumValueIndex = (int)spot.Flicker;
                    f.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>ShadowCaster2D у формі прямокутника рівня (шлях задається в локальних координатах).</summary>
        private static void AddCaster(Transform parent, Box box)
        {
            GameObject go = new GameObject("Caster");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = HeistWorldView.ToWorld(new Vec2(box.X, box.Y));
            ShadowCaster2D caster = go.AddComponent<ShadowCaster2D>();
            caster.selfShadows = false;
            SerializedObject so = new SerializedObject(caster);
            SerializedProperty path = so.FindProperty("m_ShapePath");
            Vector3[] corners =
            {
                new Vector3(0f, -box.H, 0f), new Vector3(0f, 0f, 0f), new Vector3(box.W, 0f, 0f), new Vector3(box.W, -box.H, 0f),
            };
            path.arraySize = corners.Length;
            for (int i = 0; i < corners.Length; i++)
            {
                path.GetArrayElementAtIndex(i).vector3Value = corners[i];
            }

            so.FindProperty("m_ShapePathHash").intValue = 0;
            so.FindProperty("m_ShadowCastingSource").enumValueIndex = 1; // ShapeEditor
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Центр вивіски на фасаді (координати рівня): посередині вітрин, на тротуарі під ними.</summary>
        public static readonly Vec2 StoreSignCenter = new Vec2(10.5f * HeistLevel.Tile, 15.5f * HeistLevel.Tile);

        /// <summary>Пара з вентиляційної решітки тротуару: повільні напівпрозорі клуби (життя в нижній частині кадру).</summary>
        private static void AddSteam(Transform parent, Vector3 pos)
        {
            GameObject go = new GameObject("Steam");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = 10f; // спрайт-клуб 10×10 у масштабі 1:1: сходинки альфи, піксель-арт
            main.startColor = new Color(0.95f, 0.75f, 0.88f, 0.35f); // рожеві від неону вивіски
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 6f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(8f, 2f, 1f);
            shape.rotation = new Vector3(-90f, 0f, 0f); // вгору по Y світу
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 4f;
            noise.frequency = 0.25f;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.enabled = false;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SteamMaterial();
            r.sortingOrder = 8;
        }

        private static Material SteamMaterial()
        {
            const string path = "Assets/Art/Materials/Steam.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(m, path);
            }

            // Піксельний клуб з трьома рівнями альфи (не блюр і не одиничні квадрати).
            m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Fx/steam_puff.png");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Дощ у прямокутнику рівня (x, y — лівий верхній кут, Y униз): косі струмені по 1 px, падають «на камеру» вниз екрана.</summary>
        private static void AddRain(Transform parent, Vec2 origin, Vec2 size)
        {
            GameObject go = new GameObject("Rain");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = HeistWorldView.ToWorld(new Vec2(origin.X + size.X / 2f, origin.Y));
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(size.Y / 220f, size.Y / 160f);
            main.startSize = 1f;
            main.startColor = new Color(0.76f, 0.85f, 1f, 0.32f);
            main.maxParticles = 1200;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = size.X * size.Y / 350f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.X, 1f, 1f);
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-30f);
            vel.y = new ParticleSystem.MinMaxCurve(-190f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.03f; // струмінь ~6 px
            r.lengthScale = 1f;
            r.sharedMaterial = DustMaterial();
            r.sortingOrder = 8;
        }

        /// <summary>Пил, що кружляє в промені лампи: живий кадр без «шуму» (R4).</summary>
        private static void AddDust(Transform lamp, float radius)
        {
            GameObject go = new GameObject("Dust");
            go.transform.SetParent(lamp, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = 1f;
            main.startColor = new Color(1f, 0.95f, 0.77f, 0.5f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 6f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.6f;
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 3f;
            noise.frequency = 0.3f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = DustMaterial();
            r.sortingOrder = 7;
        }

        private static Material DustMaterial()
        {
            const string path = "Assets/Art/Materials/Dust.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Sprites/Default"));
                m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Fx/pixel.png");
                AssetDatabase.CreateAsset(m, path);
            }

            return m;
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
