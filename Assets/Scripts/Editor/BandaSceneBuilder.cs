using System;
using System.Collections.Generic;
using Banda.Backdrop;
using Banda.Core;
using Banda.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Banda.EditorTools
{
    /// <summary>
    /// Збирає сцену Assets/Scenes/Main.unity: живе нічне місто (фон меню) + усі екрани інтерфейсу.
    /// Сцена генерується кодом, щоб її можна було відтворити з нуля однією командою:
    /// Banda → 2. Зібрати сцену Main. Ручні правки сцени перезапишуться при повторній збірці.
    /// </summary>
    public static partial class BandaSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        private const string LayoutPath = "Assets/Data/menu_layout.json";
        private const string VolumePath = "Assets/Settings/BandaVolumeProfile.asset";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const float CanvasW = 864f;
        private const float CanvasH = 360f;
        private static readonly Color NightLight = new Color(0.56f, 0.54f, 0.74f, 1f);
        // Глобальне світло URP додається до точкових, а CanvasModulate у Godot множив усе разом —
        // тому ніч притемнена, а точкові світла підсилені (підібрано звіркою з godot_menu_reference.png).
        private const float NightIntensity = 0.62f;
        private const float EnergyToIntensity = 0.75f;

        private static TMP_FontAsset _font;
        private static Material _litMaterial;
        private static Material _unlitMaterial;
        private static Material _additiveMaterial;

        // --- Дані макета арту (перетворені з menu_layout.json генератора Godot-версії) ---

        [Serializable]
        private class Point
        {
            public float x;
            public float y;
        }

        [Serializable]
        private class LightInfo
        {
            public string name;
            public float x;
            public float y;
            public string color;
            public float scale;
            public bool flicker;
        }

        [Serializable]
        private class LetterInfo
        {
            public string file;
            public float x;
            public float y;
        }

        [Serializable]
        private class NamedPoint
        {
            public string name;
            public float x;
            public float y;
        }

        [Serializable]
        private class GangInfo
        {
            public string name;
            public float x;
            public float y;
            public int frameW;
            public int frameH;
        }

        [Serializable]
        private class Layout
        {
            public Point[] blinkers;
            public LightInfo[] lights;
            public LetterInfo[] letters;
            public NamedPoint[] layers;
            public GangInfo[] gang;
            public Point[] steam;
            public float viaductY;
        }

        [MenuItem("Banda/2. Зібрати сцену Main", priority = 2)]
        public static void Build()
        {
            BandaAssetSetup.RunAll();
            BandaHeistArt.GenerateAndImport();
            _font = BandaAssetSetup.EnsureFontAsset();
            PrepareMaterials();
            ConfigureProject();

            Layout layout = JsonUtility.FromJson<Layout>(AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath).text);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            Light2D globalLight = BuildGlobalLight();
            BuildVolume();
            BuildSystems();
            BuildBackdrop(layout, globalLight);
            BuildHeistWorld(globalLight);
            BuildUi();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            Debug.Log($"[Banda] Сцену зібрано: {ScenePath}");
        }

        // --- Проєкт і матеріали ---------------------------------------------------------

        private static void ConfigureProject()
        {
            PlayerSettings.productName = "Банда";
            PlayerSettings.bundleVersion = "0.0.1";
            // Гра лише в альбомній орієнтації (як у Godot: window/handheld/orientation = landscape).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.supportsHDR = true; // неон яскравіший за білий → його підхоплює Bloom
                EditorUtility.SetDirty(urp);
            }
        }

        private static void PrepareMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Materials");
            }

            _litMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            _unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            _additiveMaterial = EnsureMaterial("Additive", "Legacy Shaders/Particles/Additive", null);
        }

        private static Material EnsureMaterial(string name, string shaderName, Texture texture)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find(shaderName);
            if (texture != null)
            {
                material.mainTexture = texture;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        // --- Камера, світло, пост-обробка -----------------------------------------------

        private static void BuildCamera()
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CanvasH / 2f; // базова висота 360 px, ширина розширюється (як stretch=expand)
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Night0;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }

        private static Light2D BuildGlobalLight()
        {
            GameObject go = new GameObject("Global Light 2D (ніч)");
            Light2D light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = NightLight;
            light.intensity = NightIntensity;
            light.targetSortingLayers = AllSortingLayers();
            return light;
        }

        private static void BuildVolume()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }

            // Glow неону (у Godot: WorldEnvironment glow, поріг 0.9, інтенсивність 0.4).
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.55f);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }

            // Віньєтка: кути кадру не мертві, а свідомо притемнені.
            vignette.color.Override(Palette.Night0);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            GameObject go = new GameObject("Post FX Volume");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static void BuildSystems()
        {
            GameObject systems = new GameObject("Systems");
            systems.AddComponent<GameState>();
            systems.AddComponent<Banda.HeistRuntime.HeistDirector>();
            GameObject soundGo = new GameObject("UiSound");
            soundGo.transform.SetParent(systems.transform);
            AudioSource source = soundGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            UiSound sound = soundGo.AddComponent<UiSound>();
            SetRef(sound, "_click", LoadAudio("ui_click.wav"));
            SetRef(sound, "_deny", LoadAudio("ui_deny.wav"));

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        // --- Фон: нічне місто з даху -----------------------------------------------------

        private static void BuildBackdrop(Layout layout, Light2D globalLight)
        {
            Dictionary<string, Vector2> offsets = new Dictionary<string, Vector2>();
            foreach (NamedPoint p in layout.layers)
            {
                offsets[p.name] = new Vector2(p.x, p.y);
            }

            GameObject root = new GameObject("CityBackdrop");
            Transform sky = Child(root.transform, "Sky");
            Transform far = Child(root.transform, "FarLayer");
            Transform mid = Child(root.transform, "MidLayer");
            Transform roof = Child(root.transform, "RoofLayer");
            Transform fg = Child(root.transform, "FgLayer");

            // Небо й далеке місто світяться самі — неосвітлювані (Unlit).
            Sprite(sky, "Sky", "Menu/sky", Vector2.zero, 0, false);
            Transform clouds = Child(sky, "Clouds");
            Vector2 cloudsAt = offsets["clouds"];
            Sprite(clouds, "CloudsA", "Menu/clouds", cloudsAt, 1, false);
            Sprite(clouds, "CloudsB", "Menu/clouds", cloudsAt + new Vector2(CanvasW, 0f), 1, false);
            clouds.gameObject.AddComponent<LoopScroller>();

            SpriteRenderer skyFlash = Sprite(sky, "SkyFlash", "Fx/pixel", Vector2.zero, 4, false);
            skyFlash.transform.localScale = new Vector3(CanvasW, CanvasH, 1f);
            skyFlash.sharedMaterial = _additiveMaterial;
            skyFlash.color = new Color(0.78f, 0.81f, 0.87f, 0f);
            SpriteRenderer bolt = Sprite(sky, "Bolt", "Fx/bolt", new Vector2(100f, 0f), 3, false);
            bolt.sharedMaterial = _additiveMaterial;

            Sprite(far, "Far", "Menu/far", offsets["far"], 2, false);
            Transform blinkers = Child(far, "Blinkers");
            foreach (Point p in layout.blinkers)
            {
                SpriteRenderer b = Sprite(blinkers, "Blinker", "Fx/blinker", new Vector2(p.x - 1f, p.y - 1f), 5, false);
                b.gameObject.AddComponent<Blinker>();
            }

            // Середній план: віадук з потягом, будинки, неонова вивіска.
            Sprite(mid, "Viaduct", "Menu/viaduct", offsets["viaduct"], 10, true);
            Transform train = Child(mid, "Train");
            train.localPosition = new Vector3(0f, CanvasH / 2f - layout.viaductY, 0f);
            Transform trainBody = Child(train, "Body");
            SpriteRenderer trainSprite = Sprite(trainBody, "Wagons", "Menu/train", Vector2.zero, 11, true);
            trainSprite.transform.localPosition = new Vector3(-105f, 0f, 0f);
            SpriteRenderer trainGlow = Sprite(trainBody, "Windows", "Menu/train_glow", Vector2.zero, 12, false);
            trainGlow.transform.localPosition = new Vector3(-105f, 0f, 0f);
            TrainPass trainPass = train.gameObject.AddComponent<TrainPass>();
            SetRef(trainPass, "_body", trainBody.gameObject);

            BuildRain(mid, "RainFar", "rain_far", 200f, 240, 0.9f, 380f, 440f, 0.12f, new Vector2(1f, 6f), 0.4f, 13, "Default");

            Sprite(mid, "Mid", "Menu/mid", offsets["mid"], 14, true);
            Sprite(mid, "MidGlow", "Menu/mid_glow", offsets["mid_glow"], 15, false);
            Sprite(mid, "SignFrame", "Menu/sign_frame", offsets["sign_frame"], 16, true);

            Transform letters = Child(mid, "SignLetters");
            List<SpriteRenderer> letterRenderers = new List<SpriteRenderer>();
            List<SpriteRenderer> letterCores = new List<SpriteRenderer>();
            foreach (LetterInfo info in layout.letters)
            {
                string file = info.file.Replace(".png", string.Empty);
                SpriteRenderer letter = Sprite(letters, file, "Menu/" + file, new Vector2(info.x, info.y), 17, false);
                letterRenderers.Add(letter);
                // Розпечене біле ядро трубки: адитивна копія поверх (Godot: self_modulate 1.3 у HDR + glow).
                SpriteRenderer core = Sprite(letter.transform, "Core", "Menu/" + file, Vector2.zero, 18, false);
                core.transform.localPosition = Vector3.zero;
                core.sharedMaterial = _additiveMaterial;
                core.color = new Color(1f, 0.85f, 0.92f, 0.35f);
                letterCores.Add(core);
            }

            // Дах, на якому стоїть банда.
            Sprite(roof, "Roof", "Menu/roof", offsets["roof"], 20, true);
            SpriteRenderer roofGlow = Sprite(roof, "RoofGlow", "Menu/roof_glow", offsets["roof_glow"], 21, false);
            BuildSplashes(roof);

            Light2D signLight = null;
            Light2D bounceLight = null;
            SpriteRenderer signHalo = null;
            int[] world = Layers("Default", "Front");
            int[] worldAndGang = Layers("Default", "Gang", "Front");
            foreach (LightInfo info in layout.lights)
            {
                Color color = Palette.Hex(info.color);
                Vector2 at = new Vector2(info.x, info.y);
                switch (info.name)
                {
                    case "sign":
                        // Неон світить на каркас і будинок; банду не заливає — зі спини вони силуети.
                        // Енергія вища за Godot (2.0): карти нормалей URP гасять світло на вертикалях каркаса.
                        signLight = PointLight(mid, "SignLight", at, color, 3.6f, 2.0f, 60f, world);
                        // Каркас — тонкі вертикальні рейки: з картою нормалей URP світло зверху їх майже не чіпає.
                        // Як у Godot-референсі, рейки мають ловити рожевий неон — світимо без нормалей.
                        DisableNormalMap(signLight);
                        // Відсвіт на карнизі й верхніх вікнах під вивіскою (у референсі — рожева смуга по карнизу).
                        bounceLight = PointLight(mid, "SignBounce", at + new Vector2(0f, 70f), color, 2.4f, 1.6f, 24f, world);
                        DisableNormalMap(bounceLight);
                        // Рожеве марево довкола вивіски: у мокрому повітрі неон світиться плямою.
                        signHalo = Sprite(mid, "SignHalo", "Fx/radial", at, 18, false);
                        signHalo.sharedMaterial = _additiveMaterial;
                        signHalo.transform.localScale = new Vector3(3f, 1.2f, 1f);
                        // Legacy Additive подвоює колір, тому альфа нижча, ніж у Godot (0.32).
                        signHalo.color = new Color(color.r, color.g, color.b, 0.07f);
                        break;
                    case "bulb":
                        Light2D bulb = PointLight(roof, "DoorBulb", at, color, 2.8f, 3.2f, 24f, worldAndGang);
                        SetEnum(bulb.gameObject.AddComponent<FlickerLight>(), "_kind", (int)FlickerLight.Kind.Buzz);
                        break;
                    case "billboard":
                        // Холодне світло реклами: на парапет і дах перед бандою; для банди — контрове.
                        PointLight(roof, "Billboard", at + new Vector2(0f, 46f), color, 2.6f, 2.0f, 30f, world);
                        break;
                    case "bar":
                        Light2D bar = PointLight(mid, "BarNeon", at, color, 1.3f, 1.0f, 24f, world);
                        SetEnum(bar.gameObject.AddComponent<FlickerLight>(), "_kind", (int)FlickerLight.Kind.Neon);
                        break;
                }
            }

            NeonSign sign = letters.gameObject.AddComponent<NeonSign>();
            SerializedObject signSo = new SerializedObject(sign);
            SerializedProperty letterArray = signSo.FindProperty("_letters");
            letterArray.arraySize = letterRenderers.Count;
            for (int i = 0; i < letterRenderers.Count; i++)
            {
                SerializedProperty element = letterArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Renderer").objectReferenceValue = letterRenderers[i];
                // «Д» (4-та літера) — стара трубка, збоїть частіше за інші.
                element.FindPropertyRelative("GlitchChance").floatValue = i == 3 ? 0.35f : 0.03f;
                element.FindPropertyRelative("Core").objectReferenceValue = letterCores[i];
                element.FindPropertyRelative("CoreAlpha").floatValue = 0.35f;
            }

            signSo.FindProperty("_signLight").objectReferenceValue = signLight;
            signSo.FindProperty("_bounceLight").objectReferenceValue = bounceLight;
            signSo.FindProperty("_roofGlow").objectReferenceValue = roofGlow;
            signSo.FindProperty("_halo").objectReferenceValue = signHalo;
            signSo.ApplyModifiedPropertiesWithoutUndo();

            BuildGang(roof, layout);
            if (layout.steam != null)
            {
                foreach (Point p in layout.steam)
                {
                    // Пара з вентиляції на даху.
                    Steam(roof, "VentSteam", ToWorld(new Vector2(p.x, p.y)), 26, new Vector2(8f, 2f), 1f, 3.2f, 0.45f, 24, "Default");
                }
            }

            // Передній план і ближній дощ — на окремому шарі «Front», над бандою.
            SpriteRenderer fgSprite = Sprite(fg, "Fg", "Menu/fg", offsets["fg"], 0, true);
            fgSprite.sortingLayerName = "Front";
            BuildRain(fg, "RainNear", "rain_near", 210f, 110, 0.7f, 560f, 640f, 0.14f, new Vector2(1f, 11f), 0.55f, 1, "Front");

            BuildAmbience(root.transform, globalLight, bolt, skyFlash);

            BackdropParallax parallax = root.AddComponent<BackdropParallax>();
            SerializedObject parallaxSo = new SerializedObject(parallax);
            SerializedProperty layers = parallaxSo.FindProperty("_layers");
            (Transform target, float amount)[] plan = { (sky, 0f), (far, 1f), (mid, 2f), (roof, 4f), (fg, 7f) };
            layers.arraySize = plan.Length;
            for (int i = 0; i < plan.Length; i++)
            {
                layers.GetArrayElementAtIndex(i).FindPropertyRelative("Target").objectReferenceValue = plan[i].target;
                layers.GetArrayElementAtIndex(i).FindPropertyRelative("Amount").floatValue = plan[i].amount;
            }

            parallaxSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildGang(Transform roof, Layout layout)
        {
            Transform gangRoot = Child(roof, "Gang");
            string[] order = { "runner", "leader", "hacker" };
            float[] fps = { 3.2f, 2.8f, 3.6f };
            for (int i = 0; i < order.Length; i++)
            {
                GangInfo info = Array.Find(layout.gang, g => g.name == order[i]);
                if (info == null)
                {
                    continue;
                }

                GameObject go = new GameObject("Gang_" + info.name);
                go.transform.SetParent(gangRoot, false);
                go.transform.localPosition = ToWorld(new Vector2(info.x, info.y));
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = _litMaterial;
                renderer.sortingLayerName = "Gang";
                renderer.sortingOrder = i;
                SpriteSheetAnimator animator = go.AddComponent<SpriteSheetAnimator>();
                SerializedObject so = new SerializedObject(animator);
                so.FindProperty("_sheet").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>($"{BandaAssetSetup.MenuArt}/gang_{info.name}.png");
                so.FindProperty("_frameSize").vector2IntValue = new Vector2Int(info.frameW, info.frameH);
                so.FindProperty("_fps").floatValue = fps[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                float scaleK = info.frameH / 76f;
                if (info.name == "hacker")
                {
                    // Телефон у руці хакерки: холодна пляма на обличчі й руках.
                    Light2D phone = PointLight(go.transform, "PhoneLight", Vector2.zero, Palette.Cyan2, 0.8f, 2.0f, 24f,
                        Layers("Default", "Gang", "Front"));
                    phone.transform.localPosition = new Vector3(Mathf.Round(7f * scaleK), -Mathf.Round(31f * scaleK), 0f);
                }

                if (info.name == "runner")
                {
                    // Цигарка: вогник із повільними затяжками й дим.
                    Light2D ember = PointLight(go.transform, "Ember", Vector2.zero, Palette.Lamp2, 0.14f, 0.5f, 24f,
                        Layers("Default", "Gang", "Front"));
                    ember.transform.localPosition = new Vector3(5f, -54f, 0f);
                    ember.gameObject.AddComponent<EmberPulse>();
                    Steam(go.transform, "Smoke", new Vector3(5f, -53f, 0f), 10, Vector2.zero, 0.5f, 2.6f, 0.5f, 10, "Gang");
                }

                if (info.name == "leader")
                {
                    // Ліхтарик: конус уперед-праворуч на дах. Найяскравіша пляма біля фокусу.
                    float k = info.frameH / 76f;
                    GameObject flash = new GameObject("Flashlight");
                    flash.transform.SetParent(go.transform, false);
                    flash.transform.localPosition = new Vector3(Mathf.Round(51f * k), -Mathf.Round(44f * k), 0f);
                    Light2D light = flash.AddComponent<Light2D>();
                    light.lightType = Light2D.LightType.Point;
                    light.color = Palette.Lamp4;
                    light.intensity = 3.2f * EnergyToIntensity;
                    light.pointLightOuterRadius = 64f * 1.6f;
                    light.pointLightInnerRadius = 4f;
                    light.pointLightOuterAngle = 46f;
                    light.pointLightInnerAngle = 18f;
                    light.falloffIntensity = 0.6f;
                    light.targetSortingLayers = Layers("Default", "Gang", "Front");
                    SetNormalMap(light, 70f);
                    flash.AddComponent<FlashlightSway>();
                }
            }
        }

        private static void BuildAmbience(Transform root, Light2D globalLight, SpriteRenderer bolt, SpriteRenderer skyFlash)
        {
            Transform audio = Child(root, "Ambience");
            AmbientLoop(audio, "Rain", "rain_loop.ogg", 0.63f);
            AmbientLoop(audio, "City", "city_loop.ogg", 0.4f);
            AmbientLoop(audio, "NeonHum", "neon_hum.ogg", 0.08f);
            GameObject thunderGo = new GameObject("Thunder");
            thunderGo.transform.SetParent(audio, false);
            AudioSource thunder = thunderGo.AddComponent<AudioSource>();
            thunder.clip = LoadAudio("thunder.ogg");
            thunder.playOnAwake = false;
            thunder.volume = 0.7f;

            LightningController lightning = root.gameObject.AddComponent<LightningController>();
            SetRef(lightning, "_globalLight", globalLight);
            SetRef(lightning, "_bolt", bolt);
            SetRef(lightning, "_skyFlash", skyFlash);
            SetRef(lightning, "_thunder", thunder);
        }

        private static void AmbientLoop(Transform parent, string name, string file, float volume)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = LoadAudio(file);
            source.loop = true;
            source.playOnAwake = true;
            source.volume = volume;
        }

        private static void BuildRain(Transform parent, string name, string texture, float topY, int amount, float lifetime,
            float speedMin, float speedMax, float slant, Vector2 size, float alpha, int order, string sortingLayer)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, topY, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 1f;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = size.x;
            main.startSizeY = size.y;
            main.startSizeZ = 1f;
            // Крапля нахилена вздовж руху (вітер праворуч).
            main.startRotation = -Mathf.Atan(slant);
            main.startColor = new Color(1f, 1f, 1f, alpha);
            main.maxParticles = amount + 40;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = amount / lifetime;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(940f, 1f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(speedMin * slant, speedMax * slant);
            velocity.y = new ParticleSystem.MinMaxCurve(-speedMax, -speedMin);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = FadeGradient(new[] { 0f, 0.1f, 0.85f, 1f }, new[] { 0f, 1f, 1f, 0f });

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = EnsureMaterial("Rain_" + texture, "Sprites/Default", LoadTexture("Fx/" + texture));
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
        }

        /// <summary>Пара / дим: клуби з puff.png повільно піднімаються й зносяться вітром праворуч.</summary>
        private static void Steam(Transform parent, string name, Vector3 localPos, int amount, Vector2 area, float sizeMin,
            float lifetime, float alpha, int order, string sortingLayer)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f * sizeMin, 12f * sizeMin);
            main.startColor = Color.white;
            main.maxParticles = amount + 4;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = amount / lifetime;
            ParticleSystem.ShapeModule shape = ps.shape;
            if (area == Vector2.zero)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 15f;
                shape.radius = 0.1f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(area.x, area.y, 0f);
            }

            // Конус/коробка випускають уздовж локальної +Z — розвертаємо вгору з нахилом за вітром.
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.ForceOverLifetimeModule force = ps.forceOverLifetime;
            force.enabled = true;
            force.x = new ParticleSystem.MinMaxCurve(4f);
            force.y = new ParticleSystem.MinMaxCurve(8f);
            force.z = new ParticleSystem.MinMaxCurve(0f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Palette.Steel2, 0f), new GradientColorKey(Palette.Steel2, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = EnsureMaterial("Steam_puff", "Sprites/Default", LoadTexture("Fx/puff"));
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
        }

        private static void BuildSplashes(Transform roof)
        {
            GameObject go = new GameObject("Splashes");
            go.transform.SetParent(roof, false);
            go.transform.localPosition = new Vector3(0f, -158f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 0.25f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = 5f;
            main.startSizeY = 3f;
            main.startSizeZ = 1f;
            main.startColor = new Color(1f, 1f, 1f, 0.5f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 160f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(864f, 44f, 0f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(new[] { 0f, 0.5f, 1f }, new[] { 0.9f, 0.6f, 0f });
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = EnsureMaterial("Rain_splash", "Sprites/Default", LoadTexture("Fx/splash"));
            renderer.sortingOrder = 22;
        }

        // --- Інтерфейс --------------------------------------------------------------------

        private static void BuildUi()
        {
            GameObject canvasGo = new GameObject("UiCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject menu = BuildMenuScreen(canvasGo.transform);
            GameObject join = BuildJoinScreen(canvasGo.transform);
            GameObject lobby = BuildLobbyScreen(canvasGo.transform);
            GameObject heist = BuildHeistScreen(canvasGo.transform);
            GameObject result = BuildResultScreen(canvasGo.transform);
            (CanvasGroup toastGroup, TMP_Text toastLabel) = BuildToast(canvasGo.transform);

            ScreenRouter router = canvasGo.AddComponent<ScreenRouter>();
            SerializedObject so = new SerializedObject(router);
            so.FindProperty("_menuScreen").objectReferenceValue = menu;
            so.FindProperty("_joinScreen").objectReferenceValue = join;
            so.FindProperty("_lobbyScreen").objectReferenceValue = lobby;
            so.FindProperty("_heistScreen").objectReferenceValue = heist;
            so.FindProperty("_resultScreen").objectReferenceValue = result;
            so.FindProperty("_toastGroup").objectReferenceValue = toastGroup;
            so.FindProperty("_toastLabel").objectReferenceValue = toastLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            join.SetActive(false);
            lobby.SetActive(false);
            heist.SetActive(false);
            result.SetActive(false);
        }

        private static GameObject BuildMenuScreen(Transform canvas)
        {
            GameObject screen = Screen(canvas, "MenuScreen");

            // М'яка тінь під панеллю: UI стоїть на спокійній темній ділянці кадру.
            Image shade = UiImage(screen.transform, "Shade", LoadSprite("Fx/radial"), false);
            shade.color = new Color(Palette.Night0.r, Palette.Night0.g, Palette.Night0.b, 0.85f);
            shade.raycastTarget = false;
            Place(shade.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-145f, -100f), new Vector2(380f, 380f));

            RectTransform panel = Panel(screen.transform, "Panel", 8);
            // У Godot панель (offset_top 24) росла в обидва боки на надлишок вмісту — фактичний верх ≈ 16 px.
            Place(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -16f), new Vector2(166f, 0f));

            TMP_Text nameTitle = Label(panel, "NameTitle", "ТВОЄ ІМ'Я", 8, Palette.Steel2, TextAlignmentOptions.Left);
            TMP_InputField nameEdit = InputField(panel, "NameEdit", "введи ім'я", 28f, 14, TMP_InputField.ContentType.Standard);
            Button host = UiButton(panel, "HostButton", "СТВОРИТИ КІМНАТУ", 30f, false);
            Button join = UiButton(panel, "JoinButton", "ПРИЄДНАТИСЯ", 30f, false);
            Button sensors = UiButton(panel, "SensorsButton", "ТЕСТ ДАТЧИКІВ", 24f, true);

            TMP_Text version = Label(screen.transform, "VersionLabel", "v0.0.1 · етап 1", 8, Palette.Steel2, TextAlignmentOptions.Right);
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 8f), new Vector2(192f, 10f));

            MainMenuScreen logic = screen.AddComponent<MainMenuScreen>();
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("_nameEdit").objectReferenceValue = nameEdit;
            so.FindProperty("_hostButton").objectReferenceValue = host;
            so.FindProperty("_joinButton").objectReferenceValue = join;
            so.FindProperty("_sensorsButton").objectReferenceValue = sensors;
            so.FindProperty("_nameTitle").objectReferenceValue = nameTitle;
            so.FindProperty("_versionLabel").objectReferenceValue = version;
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }

        private static GameObject BuildJoinScreen(Transform canvas)
        {
            GameObject screen = Screen(canvas, "JoinScreen");
            Button back = UiButton(screen.transform, "BackButton", "< НАЗАД", 28f, true);
            Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(80f, 28f));

            RectTransform panel = Panel(screen.transform, "Panel", 0);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(540f, 280f));
            Destroy<ContentSizeFitter>(panel);
            Object.DestroyImmediate(panel.GetComponent<VerticalLayoutGroup>());
            HorizontalLayoutGroup columns = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            columns.padding = new RectOffset(10, 10, 10, 10);
            columns.spacing = 16f;
            columns.childControlWidth = true;
            columns.childControlHeight = true;
            columns.childForceExpandWidth = true;
            columns.childForceExpandHeight = true;

            RectTransform left = Column(panel, "Left", 10f);
            Label(left, "CodeTitle", "КОД КІМНАТИ", 8, Palette.Steel2, TextAlignmentOptions.Left);
            TMP_InputField code = InputField(left, "CodeEdit", "····", 44f, 4, TMP_InputField.ContentType.IntegerNumber);
            code.textComponent.fontSize = 24f;
            ((TMP_Text)code.placeholder).fontSize = 24f;
            TMP_Text status = Label(left, "StatusLabel", "Введи код з екрана хоста", 8, Palette.Muted, TextAlignmentOptions.TopLeft);
            status.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
            Spacer(left);
            Label(left, "IpTitle", "не знаходить? IP хоста:", 8, Palette.Steel2, TextAlignmentOptions.Left);
            RectTransform ipRow = Row(left, "IpRow", 6f);
            TMP_InputField ip = InputField(ipRow, "IpEdit", "192.168.0.10", 28f, 15, TMP_InputField.ContentType.Standard);
            ip.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Button ipButton = UiButton(ipRow, "IpButton", "OK", 28f, false);
            ipButton.GetComponent<LayoutElement>().preferredWidth = 48f;

            RectTransform right = Column(panel, "Right", 10f);
            Label(right, "RoomsTitle", "КІМНАТИ ПОРУЧ", 8, Palette.Steel2, TextAlignmentOptions.Left);
            TMP_Text roomsHint = Label(right, "RoomsHint", "пошук кімнат…", 8, Palette.Muted, TextAlignmentOptions.Center);
            Spacer(right);

            JoinRoomScreen logic = screen.AddComponent<JoinRoomScreen>();
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("_codeEdit").objectReferenceValue = code;
            so.FindProperty("_statusLabel").objectReferenceValue = status;
            so.FindProperty("_roomsHint").objectReferenceValue = roomsHint;
            so.FindProperty("_ipEdit").objectReferenceValue = ip;
            so.FindProperty("_ipButton").objectReferenceValue = ipButton;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }

        private static GameObject BuildLobbyScreen(Transform canvas)
        {
            GameObject screen = Screen(canvas, "LobbyScreen");
            RectTransform panel = Panel(screen.transform, "Panel", 0);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 328f));
            Destroy<ContentSizeFitter>(panel);
            Object.DestroyImmediate(panel.GetComponent<VerticalLayoutGroup>());
            HorizontalLayoutGroup columns = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            columns.padding = new RectOffset(10, 10, 10, 10);
            columns.spacing = 16f;
            columns.childControlWidth = true;
            columns.childControlHeight = true;
            columns.childForceExpandWidth = false;
            columns.childForceExpandHeight = true;

            RectTransform left = Column(panel, "Left", 8f);
            LayoutElement leftLayout = left.GetComponent<LayoutElement>();
            leftLayout.preferredWidth = 230f;
            leftLayout.flexibleWidth = 0f;
            Label(left, "CodeTitle", "КОД КІМНАТИ", 8, Palette.Steel2, TextAlignmentOptions.Left);
            TMP_Text codeLabel = Label(left, "CodeLabel", "····", 32, Palette.Lamp3, TextAlignmentOptions.Left);
            TMP_Text ipLabel = Label(left, "IpLabel", "IP", 8, Palette.Muted, TextAlignmentOptions.Left);
            Label(left, "BandTitle", "БАНДА", 8, Palette.Steel2, TextAlignmentOptions.Left);
            RectTransform players = Column(left, "PlayersBox", 8f);
            players.GetComponent<LayoutElement>().flexibleHeight = 0f;
            GameObject rowTemplate = PlayerRowTemplate(players);
            Spacer(left);
            Button leave = UiButton(left, "LeaveButton", "ВИЙТИ", 30f, true);

            RectTransform right = Column(panel, "Right", 6f);
            right.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Label(right, "RoleTitle", "ТВОЯ РОЛЬ", 8, Palette.Steel2, TextAlignmentOptions.Left);
            RectTransform roles = Column(right, "RolesBox", 6f);
            roles.GetComponent<LayoutElement>().flexibleHeight = 0f;
            Button roleTemplate = UiButton(roles, "RoleButtonTemplate", "РОЛЬ", 30f, false);
            TMP_Text roleTemplateLabel = roleTemplate.GetComponentInChildren<TMP_Text>();
            roleTemplateLabel.alignment = TextAlignmentOptions.Left;
            roleTemplateLabel.margin = new Vector4(10f, 0f, 6f, 0f);
            Spacer(right);
            TMP_Text hint = Label(right, "HintLabel", string.Empty, 8, Palette.Muted, TextAlignmentOptions.Left);
            Button start = UiButton(right, "StartButton", "ПОЧАТИ СПРАВУ", 34f, false);

            LobbyScreen logic = screen.AddComponent<LobbyScreen>();
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("_codeLabel").objectReferenceValue = codeLabel;
            so.FindProperty("_ipLabel").objectReferenceValue = ipLabel;
            so.FindProperty("_playersBox").objectReferenceValue = players;
            so.FindProperty("_playerRowTemplate").objectReferenceValue = rowTemplate;
            so.FindProperty("_rolesBox").objectReferenceValue = roles;
            so.FindProperty("_roleButtonTemplate").objectReferenceValue = roleTemplate;
            so.FindProperty("_startButton").objectReferenceValue = start;
            so.FindProperty("_leaveButton").objectReferenceValue = leave;
            so.FindProperty("_hintLabel").objectReferenceValue = hint;
            so.FindProperty("_selectedSprite").objectReferenceValue = LoadUiSprite("fill");
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }

        private static (CanvasGroup, TMP_Text) BuildToast(Transform canvas)
        {
            GameObject go = new GameObject("Toast", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(40f, 14f);
            rect.offsetMax = new Vector2(-40f, 40f);
            Image back = go.AddComponent<Image>();
            back.sprite = LoadUiSprite("panel");
            back.type = Image.Type.Sliced;
            back.raycastTarget = false;
            CanvasGroup group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            TMP_Text label = Label(go.transform, "Text", string.Empty, 8, Palette.Lamp3, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 6f);
            go.SetActive(false);
            return (group, label);
        }

        // --- Помічники інтерфейсу -------------------------------------------------------

        private static GameObject Screen(Transform canvas, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            Stretch((RectTransform)go.transform, 0f);
            return go;
        }

        private static RectTransform Panel(Transform parent, string name, float spacing)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.sprite = LoadUiSprite("panel");
            image.type = Image.Type.Sliced;
            // Тінь панелі (у Godot — окрема PanelShadow зі зсувом 6 px).
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(6f, -6f);
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return (RectTransform)go.transform;
        }

        private static RectTransform Column(Transform parent, string name, float spacing)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            LayoutElement element = go.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.flexibleHeight = 1f;
            return (RectTransform)go.transform;
        }

        private static RectTransform Row(Transform parent, string name, float spacing)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return (RectTransform)go.transform;
        }

        private static void Spacer(Transform parent)
        {
            GameObject go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private static GameObject PlayerRowTemplate(Transform parent)
        {
            RectTransform row = Row(parent, "PlayerRowTemplate", 6f);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            Image dot = UiImage(row, "Dot", LoadSprite("Fx/pixel"), false);
            LayoutElement dotLayout = dot.gameObject.AddComponent<LayoutElement>();
            dotLayout.preferredWidth = 6f;
            dotLayout.preferredHeight = 6f;
            dotLayout.minWidth = 6f;
            TMP_Text nameLabel = Label(row, "Name", "Гравець", 8, Palette.Text, TextAlignmentOptions.Left);
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            nameLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Label(row, "Role", "обирає…", 8, Palette.Muted, TextAlignmentOptions.Right);
            return row.gameObject;
        }

        private static TMP_Text Label(Transform parent, string name, string text, int size, Color color, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.text = text;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.lineSpacing = 50f; // як line_spacing 4 px у темі Godot
            label.raycastTarget = false;
            return label;
        }

        private static Image UiImage(Transform parent, string name, Sprite sprite, bool sliced)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        private static Button UiButton(Transform parent, string name, string text, float height, bool secondary)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.sprite = LoadUiSprite(secondary ? "button_secondary" : "button_normal");
            image.type = Image.Type.Sliced;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = LoadUiSprite(secondary ? "button_secondary_hover" : "button_hover"),
                pressedSprite = LoadUiSprite(secondary ? "button_secondary_hover" : "button_pressed"),
                selectedSprite = null,
                disabledSprite = LoadUiSprite("button_disabled"),
            };
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None; // сенсорний екран: без «залиплого» виділення
            button.navigation = nav;
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            TMP_Text label = Label(go.transform, "Label", text, 8, secondary ? Palette.Cyan2 : Palette.Lamp3, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;

            UiButtonFeedback feedback = go.AddComponent<UiButtonFeedback>();
            SerializedObject so = new SerializedObject(feedback);
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_normalColor").colorValue = secondary ? Palette.Cyan2 : Palette.Lamp3;
            so.FindProperty("_hoverColor").colorValue = secondary ? Color.white : Palette.Lamp4;
            so.FindProperty("_disabledColor").colorValue = Palette.Steel2;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        private static TMP_InputField InputField(Transform parent, string name, string placeholder, float height, int limit,
            TMP_InputField.ContentType contentType)
        {
            TMP_DefaultControls.Resources resources = new TMP_DefaultControls.Resources
            {
                inputField = LoadUiSprite("edit_normal"),
            };
            GameObject go = TMP_DefaultControls.CreateInputField(resources);
            go.name = name;
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = LoadUiSprite("edit_normal");
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            TMP_InputField field = go.GetComponent<TMP_InputField>();
            field.transition = Selectable.Transition.SpriteSwap;
            field.spriteState = new SpriteState
            {
                highlightedSprite = LoadUiSprite("edit_focus"),
                pressedSprite = LoadUiSprite("edit_focus"),
                selectedSprite = LoadUiSprite("edit_focus"),
                disabledSprite = LoadUiSprite("edit_normal"),
            };
            field.characterLimit = limit;
            field.contentType = contentType;
            field.caretColor = Palette.Cyan2;
            field.customCaretColor = true;
            field.selectionColor = Palette.Night5;
            field.fontAsset = _font;
            field.pointSize = 8f;
            RectTransform area = field.textViewport;
            area.offsetMin = new Vector2(8f, 4f);
            area.offsetMax = new Vector2(-8f, -4f);
            TMP_Text text = field.textComponent;
            text.font = _font;
            text.fontSize = 8f;
            text.color = Palette.Steel3;
            text.alignment = TextAlignmentOptions.Center;
            TMP_Text hint = (TMP_Text)field.placeholder;
            hint.font = _font;
            hint.fontSize = 8f;
            hint.text = placeholder;
            hint.color = Palette.Steel2;
            hint.fontStyle = FontStyles.Normal;
            hint.alignment = TextAlignmentOptions.Center;
            // Стандартне поле TMP вмикає extraPadding — для растрового атласу це тягне пікселі сусідніх гліфів.
            text.extraPadding = false;
            hint.extraPadding = false;
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            return field;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Destroy<T>(Component owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component);
            }
        }

        // --- Помічники світу -------------------------------------------------------------

        /// <summary>Координати генератора арту (полотно 864×360, Y униз) → світ Unity (центр = 0, Y угору).</summary>
        private static Vector3 ToWorld(Vector2 canvas)
        {
            return new Vector3(canvas.x - CanvasW / 2f, CanvasH / 2f - canvas.y, 0f);
        }

        private static Transform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static SpriteRenderer Sprite(Transform parent, string name, string asset, Vector2 canvasPos, int order, bool lit)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = ToWorld(canvasPos);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(asset);
            renderer.sharedMaterial = lit ? _litMaterial : _unlitMaterial;
            renderer.sortingOrder = order;
            if (renderer.sprite == null)
            {
                Debug.LogError($"[Banda] Немає спрайта Assets/Art/{asset}.png");
            }

            return renderer;
        }

        private static Light2D PointLight(Transform parent, string name, Vector2 canvasPos, Color color, float textureScale,
            float energy, float height, int[] sortingLayers)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = ToWorld(canvasPos);
            Light2D light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = energy * EnergyToIntensity;
            // Текстура світла в Godot — 128 px × texture_scale, тобто радіус 64 × scale.
            light.pointLightOuterRadius = 64f * textureScale;
            light.pointLightInnerRadius = 0f;
            light.falloffIntensity = 0.65f;
            light.targetSortingLayers = sortingLayers;
            SetNormalMap(light, height);
            return light;
        }

        /// <summary>Карти нормалей: світло «над» площиною на висоті height, ковзає по цеглі.</summary>
        private static void SetNormalMap(Light2D light, float height)
        {
            SerializedObject so = new SerializedObject(light);
            SerializedProperty quality = so.FindProperty("m_NormalMapQuality");
            SerializedProperty distance = so.FindProperty("m_NormalMapDistance");
            if (quality != null)
            {
                quality.intValue = (int)Light2D.NormalMapQuality.Accurate;
            }

            if (distance != null)
            {
                distance.floatValue = height;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DisableNormalMap(Light2D light)
        {
            SerializedObject so = new SerializedObject(light);
            SerializedProperty quality = so.FindProperty("m_NormalMapQuality");
            if (quality != null)
            {
                quality.intValue = (int)Light2D.NormalMapQuality.Disabled;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static int[] Layers(params string[] names)
        {
            List<int> ids = new List<int>();
            foreach (string n in names)
            {
                int id = SortingLayer.NameToID(n);
                if (SortingLayer.IsValid(id))
                {
                    ids.Add(id);
                }
            }

            return ids.ToArray();
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

        private static ParticleSystem.MinMaxGradient FadeGradient(float[] times, float[] alphas)
        {
            Gradient gradient = new Gradient();
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                alphaKeys[i] = new GradientAlphaKey(alphas[i], times[i]);
            }

            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphaKeys);
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static Sprite LoadSprite(string relative) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/{relative}.png");

        private static Sprite LoadUiSprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{BandaAssetSetup.UiArt}/{file}.png");

        private static Texture2D LoadTexture(string relative) => AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/{relative}.png");

        private static AudioClip LoadAudio(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{file}");

        private static void SetRef(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, int value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuild()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
