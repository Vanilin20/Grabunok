using Banda.ArtGen;
using Banda.Core;
using Banda.Heist;
using Banda.HeistRuntime;
using Banda.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Banda.EditorTools
{
    /// <summary>
    /// Справа в сцені Main: світ магазину для Тіні (далеко праворуч від міста, x = WorldX),
    /// екрани ролей, спільний HUD і газетний підсумок. Усе розставляється з HeistLevel і констант генераторів арту.
    /// </summary>
    public static partial class BandaSceneBuilder
    {
        private const float WorldX = 4000f;

        private static Sprite HeistSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{BandaHeistArt.Folder}/{name}.png");

        private static Texture2D HeistTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{BandaHeistArt.Folder}/{name}.png");

        // --- Світ Тіні ---------------------------------------------------------------------------

        private static void BuildHeistWorld(Light2D globalLight)
        {
            HeistLevel level = HeistLevel.CornerStore();
            GameObject root = new GameObject("HeistWorld");
            root.transform.position = new Vector3(WorldX, 0f, 0f);
            Transform levelRoot = Child(root.transform, "Level");

            SpriteRenderer store = WorldSprite(levelRoot, "StoreBase", "store_base", new Vector3(-StoreArt.Pad, StoreArt.Pad, 0f), 0, true);
            store.sortingOrder = 0;
            SpriteRenderer glow = WorldSprite(levelRoot, "StoreGlow", "store_glow", new Vector3(-StoreArt.Pad, StoreArt.Pad, 0f), 1, false);
            glow.sharedMaterial = _additiveMaterial;
            glow.color = new Color(1f, 1f, 1f, 0.55f); // Legacy Additive подвоює — пів сили

            SpriteRenderer door = WorldSprite(levelRoot, "BackroomDoor", "door_closed",
                HeistWorldView.ToWorld(new Vec2(level.Door.X, level.Door.Y)), 3, true);
            SpriteRenderer safe = WorldSprite(levelRoot, "Safe", "safe_closed", HeistWorldView.ToWorld(level.SafePos), 3, true);
            foreach (Vec2 cam in level.Cameras)
            {
                WorldSprite(levelRoot, "CameraDome", "camera_dome", HeistWorldView.ToWorld(cam), 4, true);
                SpriteRenderer led = WorldSprite(levelRoot, "CameraLed", "led_red", HeistWorldView.ToWorld(cam) + new Vector3(2f, 2f, 0f), 5, false);
                led.gameObject.AddComponent<LedBlink>();
            }

            foreach (LightSpot spot in level.Lights)
            {
                GameObject go = new GameObject("Light_" + spot.Name);
                go.transform.SetParent(levelRoot, false);
                go.transform.localPosition = HeistWorldView.ToWorld(spot.Pos);
                Light2D light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = Palette.Hex(spot.Color);
                light.intensity = spot.Intensity * 1.6f;
                light.pointLightOuterRadius = spot.Radius;
                light.pointLightInnerRadius = 0f;
                light.falloffIntensity = 0.5f;
                light.targetSortingLayers = AllSortingLayers();
                if (spot.Flicker != LightFlicker.None)
                {
                    LevelLightFlicker flicker = go.AddComponent<LevelLightFlicker>();
                    SerializedObject so = new SerializedObject(flicker);
                    so.FindProperty("_kind").enumValueIndex = (int)spot.Flicker;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Охоронець: освітлюється, ліхтарик — конус уперед + слабкий відсвіт на ньому самому.
            CharacterSpriteAnimator guard = Character(levelRoot, "Guard", "guard_sheet", true, 5);
            Light2D flashlight = new GameObject("Flashlight").AddComponent<Light2D>();
            flashlight.transform.SetParent(guard.transform, false);
            flashlight.lightType = Light2D.LightType.Point;
            flashlight.color = Palette.Lamp4;
            flashlight.intensity = 1.9f;
            flashlight.pointLightOuterRadius = HeistSim.FlashlightRange + 8f;
            flashlight.pointLightInnerRadius = 2f;
            flashlight.pointLightOuterAngle = HeistSim.FlashlightHalfAngleDeg * 2f;
            flashlight.pointLightInnerAngle = HeistSim.FlashlightHalfAngleDeg;
            flashlight.falloffIntensity = 0.55f;
            flashlight.targetSortingLayers = AllSortingLayers();
            Light2D guardGlow = new GameObject("GuardGlow").AddComponent<Light2D>();
            guardGlow.transform.SetParent(guard.transform, false);
            guardGlow.lightType = Light2D.LightType.Point;
            guardGlow.color = Palette.Lamp4;
            guardGlow.intensity = 1.2f;
            guardGlow.pointLightOuterRadius = 14f;
            guardGlow.targetSortingLayers = AllSortingLayers();

            // Тінь: неосвітлювана (завжди читається для свого гравця) + фіолетова аура навколо.
            CharacterSpriteAnimator shadow = Character(levelRoot, "Shadow", "shadow_sheet", false, 6);
            Light2D aura = new GameObject("Aura").AddComponent<Light2D>();
            aura.transform.SetParent(shadow.transform, false);
            aura.lightType = Light2D.LightType.Point;
            aura.color = Palette.Night7;
            aura.intensity = 2.2f;
            aura.pointLightOuterRadius = 26f;
            aura.targetSortingLayers = AllSortingLayers();
            shadow.transform.localPosition = HeistWorldView.ToWorld(level.ShadowStart);

            HeistWorldView view = root.AddComponent<HeistWorldView>();
            SerializedObject v = new SerializedObject(view);
            v.FindProperty("_levelRoot").objectReferenceValue = levelRoot;
            v.FindProperty("_shadow").objectReferenceValue = shadow;
            v.FindProperty("_guard").objectReferenceValue = guard;
            v.FindProperty("_guardFlashlight").objectReferenceValue = flashlight;
            v.FindProperty("_door").objectReferenceValue = door;
            v.FindProperty("_doorClosed").objectReferenceValue = HeistSprite("door_closed");
            v.FindProperty("_doorOpen").objectReferenceValue = HeistSprite("door_open");
            v.FindProperty("_safe").objectReferenceValue = safe;
            v.FindProperty("_safeClosed").objectReferenceValue = HeistSprite("safe_closed");
            v.FindProperty("_safeOpen").objectReferenceValue = HeistSprite("safe_open");
            v.FindProperty("_globalLight").objectReferenceValue = globalLight;
            v.FindProperty("_nightColor").colorValue = Palette.Night6;
            v.FindProperty("_nightIntensity").floatValue = 0.35f;
            v.FindProperty("_menuBackdrop").objectReferenceValue = GameObject.Find("CityBackdrop");
            v.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SpriteRenderer WorldSprite(Transform parent, string name, string asset, Vector3 localPos, int order, bool lit)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = HeistSprite(asset);
            r.sharedMaterial = lit ? _litMaterial : _unlitMaterial;
            r.sortingOrder = order;
            if (r.sprite == null)
            {
                Debug.LogError($"[Banda] Немає спрайта {BandaHeistArt.Folder}/{asset}.png");
            }

            return r;
        }

        private static CharacterSpriteAnimator Character(Transform parent, string name, string sheet, bool lit, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = lit ? _litMaterial : _unlitMaterial;
            r.sortingOrder = order;
            CharacterSpriteAnimator anim = go.AddComponent<CharacterSpriteAnimator>();
            SerializedObject so = new SerializedObject(anim);
            so.FindProperty("_sheet").objectReferenceValue = HeistTexture(sheet);
            so.ApplyModifiedPropertiesWithoutUndo();
            return anim;
        }

        // --- Екрани справи -----------------------------------------------------------------------

        private static GameObject BuildHeistScreen(Transform canvas)
        {
            GameObject screen = Screen(canvas, "HeistScreen");
            BuildHackerPanel(screen);
            BuildSafePanel(screen);
            BuildShadowPanel(screen);
            BuildHud(screen);
            return screen;
        }

        /// <summary>Пристрій 640×360 по центру екрана; діти розставляються від верхнього лівого кута (x, -y).</summary>
        private static RectTransform Device(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(-320f, 180f), new Vector2(640f, 360f));
            return rt;
        }

        private static Image At(Transform parent, string name, Sprite sprite, float x, float y, bool native = true)
        {
            Image image = UiImage(parent, name, sprite, false);
            RectTransform rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            if (native && sprite != null)
            {
                image.SetNativeSize();
            }

            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text TextAt(Transform parent, string name, string text, float x, float y, float w, int size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            TMP_Text label = Label(parent, name, text, size, color, align);
            RectTransform rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, size + 4);
            return label;
        }

        private static Image FullBackground(Transform parent, string name, Color color)
        {
            Image bg = UiImage(parent, name, LoadSprite("Fx/pixel"), false);
            Stretch(bg.rectTransform, 0f);
            bg.color = color;
            bg.raycastTarget = false;
            return bg;
        }

        private static void BuildHackerPanel(GameObject screen)
        {
            GameObject panel = Screen(screen.transform, "HackerPanel");
            FullBackground(panel.transform, "Desk", Palette.Night1);
            RectTransform device = Device(panel.transform, "Laptop");
            Image display = At(device, "Display", LoadSprite("Fx/pixel"), HackerArt.ScreenX, HackerArt.ScreenY, false);
            display.rectTransform.sizeDelta = new Vector2(HackerArt.ScreenW, HackerArt.ScreenH);
            display.color = Palette.Night0;
            TextAt(device, "Title", "ОХОРОНА.EXE · МАГАЗИН НА РОЗІ", HackerArt.ScreenX + 10, HackerArt.ScreenY + 6, 400, 8, Palette.Cyan);
            Image scheme = At(device, "Scheme", HeistSprite("hacker_schematic"), HackerArt.ScreenX + HackerArt.SchemeX, HackerArt.ScreenY + HackerArt.SchemeY);
            RectTransform schemeRoot = scheme.rectTransform;

            // Шаблон кнопки камери: іконка + відлік.
            Button camTemplate = IconButton(schemeRoot, "CameraTemplate", HeistSprite("h_cam_on"), 0f, 0f);
            TMP_Text timer = TextAt(camTemplate.transform, "Timer", string.Empty, 14f, 0f, 30f, 8, Palette.Danger);
            timer.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            timer.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            Button doorButton = IconButton(schemeRoot, "DoorButton", HeistSprite("h_door_closed"), 0f, 0f);
            Image guard = At(schemeRoot, "Guard", HeistSprite("h_guard"), 0f, 0f);
            guard.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Image gaze = UiImage(guard.transform, "Gaze", LoadSprite("Fx/pixel"), false);
            gaze.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            gaze.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            gaze.rectTransform.pivot = new Vector2(0f, 0.5f);
            gaze.rectTransform.sizeDelta = new Vector2(HeistSim.FlashlightRange, 1f);
            gaze.color = new Color(Palette.Danger.r, Palette.Danger.g, Palette.Danger.b, 0.7f);
            gaze.raycastTarget = false;

            // Стрічка з камери.
            float fx = HackerArt.ScreenX + HackerArt.ScreenW - 122;
            float fy = HackerArt.ScreenY + 30;
            At(device, "FeedFrame", HeistSprite("h_feed_frame"), fx - 2, fy - 11);
            At(device, "Feed", HeistSprite("h_feed"), fx, fy - 2);
            Image noise = At(device, "FeedNoise", HeistSprite("crt_noise_0"), fx, fy - 2);
            TMP_Text feedStatus = TextAt(device, "FeedStatus", "НАЖИВО", fx, fy + 72, 104, 8, Palette.Safe, TextAlignmentOptions.Top);
            TMP_Text alert = TextAt(device, "Alert", "ОХОРОНА НАСТОРОЖІ", fx - 10, fy + 90, 124, 8, Palette.Danger, TextAlignmentOptions.Top);
            TextAt(device, "Hint", "ТАП ПО КАМЕРІ — ВИМКНУТИ НА 6 С\nТАП ПО ДВЕРЯХ — ВІДЧИНИТИ", fx - 70, HackerArt.ScreenY + 222, 190, 8, Palette.Cyan);

            // CRT: сканлайни (плиткою) і віньєтка поверх усього вмісту екрана, корпус — найвище.
            Image scan = At(device, "Scanlines", HeistSprite("crt_scan"), HackerArt.ScreenX, HackerArt.ScreenY, false);
            scan.type = Image.Type.Tiled;
            scan.rectTransform.sizeDelta = new Vector2(HackerArt.ScreenW, HackerArt.ScreenH);
            At(device, "Vignette", HeistSprite("crt_vignette"), HackerArt.ScreenX, HackerArt.ScreenY);
            At(device, "LaptopFrame", HeistSprite("laptop_frame"), 0f, 0f);

            HackerScreen logic = screen.AddComponent<HackerScreen>();
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_schemeRoot").objectReferenceValue = schemeRoot;
            so.FindProperty("_cameraTemplate").objectReferenceValue = camTemplate;
            so.FindProperty("_doorButton").objectReferenceValue = doorButton;
            so.FindProperty("_doorIcon").objectReferenceValue = doorButton.targetGraphic;
            so.FindProperty("_doorOpenSprite").objectReferenceValue = HeistSprite("h_door_open");
            so.FindProperty("_doorClosedSprite").objectReferenceValue = HeistSprite("h_door_closed");
            so.FindProperty("_camOnSprite").objectReferenceValue = HeistSprite("h_cam_on");
            so.FindProperty("_camOffSprite").objectReferenceValue = HeistSprite("h_cam_off");
            so.FindProperty("_guard").objectReferenceValue = guard.rectTransform;
            so.FindProperty("_guardGaze").objectReferenceValue = gaze.rectTransform;
            so.FindProperty("_feedNoise").objectReferenceValue = noise;
            SerializedProperty frames = so.FindProperty("_noiseFrames");
            frames.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue = HeistSprite("crt_noise_" + i);
            }

            so.FindProperty("_feedStatus").objectReferenceValue = feedStatus;
            so.FindProperty("_alertLabel").objectReferenceValue = alert;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Кнопка-іконка: центр у точці (x, -y) батька, зона дотику більша за іконку (палець ≥ 28 px).</summary>
        private static Button IconButton(Transform parent, string name, Sprite icon, float x, float y)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(28f, 28f);
            Image hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f); // невидима зона дотику
            Image image = UiImage(go.transform, "Icon", icon, false);
            image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.SetNativeSize();
            image.raycastTarget = false;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            return button;
        }

        private static void BuildSafePanel(GameObject screen)
        {
            GameObject panel = Screen(screen.transform, "SafePanel");
            FullBackground(panel.transform, "Wall", Palette.Night1);
            RectTransform device = Device(panel.transform, "Safe");
            At(device, "Front", HeistSprite("safe_front"), 0f, 0f);
            float half = SafeArt.DialSize / 2f;
            // Золотий спалах влучання — кільце під диском (на ПК замість вібрації).
            Image flash = At(device, "HitFlash", LoadSprite("Fx/radial"), SafeArt.DialCenterX - half - 18, SafeArt.DialCenterY - half - 18, false);
            flash.rectTransform.sizeDelta = new Vector2(SafeArt.DialSize + 36, SafeArt.DialSize + 36);
            flash.color = new Color(1f, 1f, 1f, 0f);
            Image dial = At(device, "Dial", null, SafeArt.DialCenterX - half, SafeArt.DialCenterY - half, false);
            dial.rectTransform.sizeDelta = new Vector2(SafeArt.DialSize, SafeArt.DialSize);
            dial.raycastTarget = true;
            PointerRelay relay = dial.gameObject.AddComponent<PointerRelay>();
            At(device, "Marker", HeistSprite("safe_marker"), SafeArt.DialCenterX - 5, SafeArt.DialCenterY - half - 10);
            Image[] lamps = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                lamps[i] = At(device, "Lamp" + i, HeistSprite("safe_lamp_off"), 250 - 30 + i * 30 - 7, 93);
            }

            At(device, "LcdBack", HeistSprite("safe_lcd"), 250 - 25, 271);
            TMP_Text lcd = TextAt(device, "Lcd", "00", 250 - 25, 275, 50, 8, Palette.Cyan, TextAlignmentOptions.Top);
            TMP_Text status = TextAt(device, "Status", string.Empty, 80, 336, 480, 8, Palette.Text);
            Button lockButton = UiButton(device, "LockButton", "ЗАФІКСУВАТИ", 26f, false);
            PlaceTopLeft((RectTransform)lockButton.transform, 370, 290, 110, 26);
            Button resetButton = UiButton(device, "ResetButton", "СКИНУТИ", 26f, true);
            PlaceTopLeft((RectTransform)resetButton.transform, 490, 290, 84, 26);

            AudioSource audio = panel.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            SafeScreen logic = screen.AddComponent<SafeScreen>();
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_dial").objectReferenceValue = dial;
            so.FindProperty("_dialInput").objectReferenceValue = relay;
            so.FindProperty("_dialSheet").objectReferenceValue = HeistTexture("safe_dial_sheet");
            so.FindProperty("_dialFrame").intValue = SafeArt.DialSize;
            SerializedProperty lampArray = so.FindProperty("_lamps");
            lampArray.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                lampArray.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            }

            so.FindProperty("_lampOff").objectReferenceValue = HeistSprite("safe_lamp_off");
            so.FindProperty("_lampOn").objectReferenceValue = HeistSprite("safe_lamp_on");
            so.FindProperty("_lampRed").objectReferenceValue = HeistSprite("safe_lamp_red");
            so.FindProperty("_hitFlash").objectReferenceValue = flash;
            so.FindProperty("_lcd").objectReferenceValue = lcd;
            so.FindProperty("_status").objectReferenceValue = status;
            so.FindProperty("_lockButton").objectReferenceValue = lockButton;
            so.FindProperty("_resetButton").objectReferenceValue = resetButton;
            so.FindProperty("_audio").objectReferenceValue = audio;
            so.FindProperty("_tick").objectReferenceValue = LoadAudio("safe_tick.wav");
            so.FindProperty("_hit").objectReferenceValue = LoadAudio("safe_hit.wav");
            so.FindProperty("_fail").objectReferenceValue = LoadAudio("ui_deny.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void BuildShadowPanel(GameObject screen)
        {
            GameObject panel = Screen(screen.transform, "ShadowPanel");
            // Ліва половина — зона джойстика (прозора, ловить дотик).
            Image zone = UiImage(panel.transform, "StickZone", LoadSprite("Fx/pixel"), false);
            zone.rectTransform.anchorMin = new Vector2(0f, 0f);
            zone.rectTransform.anchorMax = new Vector2(0.5f, 0.8f);
            zone.rectTransform.offsetMin = Vector2.zero;
            zone.rectTransform.offsetMax = Vector2.zero;
            zone.color = new Color(0f, 0f, 0f, 0f);
            PointerRelay relay = zone.gameObject.AddComponent<PointerRelay>();
            Image stickBase = UiImage(panel.transform, "StickBase", HeistSprite("stick_base"), false);
            stickBase.SetNativeSize();
            Place(stickBase.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(58f, 58f), new Vector2(56f, 56f));
            stickBase.raycastTarget = false;
            Image knob = UiImage(stickBase.transform, "Knob", HeistSprite("stick_knob"), false);
            knob.SetNativeSize();
            knob.raycastTarget = false;

            Button take = RoundAction(panel.transform, "TakeButton", "icon_take", "ВЗЯТИ", new Vector2(-74f, 34f));
            Button hide = RoundAction(panel.transform, "HideButton", "icon_hide", "СХОВАТИСЬ", new Vector2(-26f, 52f));

            ShadowControls logic = screen.AddComponent<ShadowControls>();
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_stickArea").objectReferenceValue = stickBase.rectTransform;
            so.FindProperty("_stickInput").objectReferenceValue = relay;
            so.FindProperty("_knob").objectReferenceValue = knob.rectTransform;
            so.FindProperty("_stickRadius").floatValue = 22f;
            so.FindProperty("_hideButton").objectReferenceValue = hide;
            so.FindProperty("_takeButton").objectReferenceValue = take;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Кругла кнопка дії Тіні: робоча — з фіолетовим кантом, вимкнена — тьмяна (SpriteSwap).</summary>
        private static Button RoundAction(Transform parent, string name, string icon, string caption, Vector2 fromBottomRight)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            Place(rt, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), fromBottomRight, new Vector2(40f, 40f));
            Image back = go.AddComponent<Image>();
            back.sprite = HeistSprite("btn_round_lit");
            Button button = go.AddComponent<Button>();
            button.targetGraphic = back;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { disabledSprite = HeistSprite("btn_round"), pressedSprite = HeistSprite("btn_round") };
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            Image iconImage = UiImage(go.transform, "Icon", HeistSprite(icon), false);
            iconImage.SetNativeSize();
            iconImage.raycastTarget = false;
            TMP_Text label = Label(go.transform, "Caption", caption, 8, Palette.Night7, TextAlignmentOptions.Top);
            label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            label.rectTransform.sizeDelta = new Vector2(90f, 10f);
            UiButtonFeedback feedback = go.AddComponent<UiButtonFeedback>();
            SerializedObject so = new SerializedObject(feedback);
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_normalColor").colorValue = Palette.Night7;
            so.FindProperty("_hoverColor").colorValue = Palette.Text;
            so.FindProperty("_disabledColor").colorValue = Palette.Night5;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        private static void BuildHud(GameObject screen)
        {
            GameObject root = new GameObject("Hud", typeof(RectTransform));
            root.transform.SetParent(screen.transform, false);
            Stretch((RectTransform)root.transform, 0f);
            // Шкала тривоги по центру вгорі.
            GameObject bar = new GameObject("AlarmBar", typeof(RectTransform));
            bar.transform.SetParent(root.transform, false);
            RectTransform barRt = (RectTransform)bar.transform;
            Place(barRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(236f, 24f));
            Image backing = UiImage(bar.transform, "Backing", LoadSprite("Fx/pixel"), false);
            Stretch(backing.rectTransform, 0f);
            backing.color = new Color(Palette.Night0.r, Palette.Night0.g, Palette.Night0.b, 0.66f);
            backing.raycastTarget = false;
            Image siren = At(bar.transform, "Siren", HeistSprite("hud_siren"), 4, 3);
            At(bar.transform, "Frame", HeistSprite("hud_bar_frame"), 22, 3);
            Image[] segments = new Image[20];
            for (int i = 0; i < 20; i++)
            {
                segments[i] = At(bar.transform, "Seg" + i, HeistSprite("hud_segment"), 25 + i * 8, 6, false);
                segments[i].rectTransform.sizeDelta = new Vector2(7f, 6f);
            }

            float mark = 25 + 10 * 8 - 1;
            Image threshold = At(bar.transform, "Threshold50", LoadSprite("Fx/pixel"), mark, 0, false);
            threshold.rectTransform.sizeDelta = new Vector2(1f, 16f);
            threshold.color = Palette.Lamp4;
            TextAt(bar.transform, "ThresholdLabel", "50%", mark - 11, 16, 30, 8, Palette.Lamp4);
            TMP_Text value = TextAt(bar.transform, "Value", "0%", 190, 5, 44, 8, Palette.Lamp);

            // Таймер праворуч угорі.
            GameObject timerPlate = new GameObject("Timer", typeof(RectTransform));
            timerPlate.transform.SetParent(root.transform, false);
            Image plate = timerPlate.AddComponent<Image>();
            plate.sprite = HeistSprite("hud_plate");
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;
            Place(plate.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(54f, 18f));
            At(timerPlate.transform, "Clock", HeistSprite("hud_clock"), 3, 3);
            TMP_Text timer = TextAt(timerPlate.transform, "Text", "10:00", 17, 5, 36, 8, Palette.Text);

            // Фішки гравців ліворуч угорі.
            Image[] icons = new Image[3];
            Image[] status = new Image[3];
            Image[] active = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject chip = new GameObject("Chip" + i, typeof(RectTransform));
                chip.transform.SetParent(root.transform, false);
                Image chipBack = chip.AddComponent<Image>();
                chipBack.sprite = HeistSprite("hud_chip");
                chipBack.type = Image.Type.Sliced;
                chipBack.raycastTarget = false;
                Place(chipBack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f + i * 34f, -4f), new Vector2(30f, 18f));
                icons[i] = At(chip.transform, "Icon", HeistSprite("icon_shadow"), 3, 3);
                status[i] = At(chip.transform, "Status", HeistSprite("status_busy"), 20, 4);
                active[i] = At(chip.transform, "Active", LoadSprite("Fx/pixel"), 2, 16, false);
                active[i].rectTransform.sizeDelta = new Vector2(26f, 1f);
                active[i].color = Palette.Night7;
            }

            // «Гаряче місце»: 1-2-3 під фішками.
            GameObject hot = new GameObject("HotSeat", typeof(RectTransform));
            hot.transform.SetParent(root.transform, false);
            RectTransform hotRt = (RectTransform)hot.transform;
            Place(hotRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -25f), new Vector2(240f, 14f));
            HorizontalLayoutGroup row = hot.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 3f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            string[] captions = { "1 ХАКЕР", "2 ТІНЬ", "3 СЕЙФ" };
            Button[] hotButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                hotButtons[i] = UiButton(hot.transform, "Seat" + (i + 1), captions[i], 14f, true);
                hotButtons[i].GetComponent<LayoutElement>().preferredWidth = 66f;
            }

            HeistHud hud = screen.AddComponent<HeistHud>();
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("_root").objectReferenceValue = root;
            SetArray(so.FindProperty("_segments"), segments);
            so.FindProperty("_siren").objectReferenceValue = siren;
            so.FindProperty("_alarmValue").objectReferenceValue = value;
            so.FindProperty("_timer").objectReferenceValue = timer;
            SetArray(so.FindProperty("_chipIcons"), icons);
            SetArray(so.FindProperty("_chipStatus"), status);
            SetArray(so.FindProperty("_chipActive"), active);
            so.FindProperty("_iconHacker").objectReferenceValue = HeistSprite("icon_hacker");
            so.FindProperty("_iconShadow").objectReferenceValue = HeistSprite("icon_shadow");
            so.FindProperty("_iconSafecracker").objectReferenceValue = HeistSprite("icon_safecracker");
            so.FindProperty("_statusBusy").objectReferenceValue = HeistSprite("status_busy");
            so.FindProperty("_statusDanger").objectReferenceValue = HeistSprite("status_danger");
            so.FindProperty("_statusOffline").objectReferenceValue = HeistSprite("status_offline");
            so.FindProperty("_hotSeatBar").objectReferenceValue = hot;
            SetArray(so.FindProperty("_hotSeatButtons"), hotButtons);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(SerializedProperty array, Object[] items)
        {
            array.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        // --- Підсумок: газетна шпальта ----------------------------------------------------------

        private static GameObject BuildResultScreen(Transform canvas)
        {
            GameObject screen = Screen(canvas, "ResultScreen");
            FullBackground(screen.transform, "Dark", Palette.Night1);
            RectTransform device = Device(screen.transform, "Desk");
            At(device, "Table", HeistSprite("news_table"), 0f, 0f);
            float px = 320 - NewspaperArt.PageW / 2f;
            float py = 18f;
            Image pageShadow = At(device, "PageShadow", LoadSprite("Fx/pixel"), px + 6, py + 6, false);
            pageShadow.rectTransform.sizeDelta = new Vector2(NewspaperArt.PageW, NewspaperArt.PageH);
            pageShadow.color = new Color(0f, 0f, 0f, 0.55f);
            At(device, "Page", HeistSprite("news_page"), px, py);
            TMP_Text headline = TextAt(device, "Headline", "…", px + 12, py + NewspaperArt.HeadlineY + 4, NewspaperArt.PageW - 24, 16,
                Palette.Night0, TextAlignmentOptions.Top);
            headline.rectTransform.sizeDelta = new Vector2(NewspaperArt.PageW - 24, NewspaperArt.HeadlineH);
            headline.textWrappingMode = TextWrappingModes.Normal;
            TMP_Text stats = TextAt(device, "Stats", string.Empty, px + 196, py + 104, 150, 8, Palette.Night1);
            stats.rectTransform.sizeDelta = new Vector2(150f, 60f);
            Image stamp = At(device, "Stamp", HeistSprite("news_stamp_win"), px + NewspaperArt.PageW - 150, py + 196);
            stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            Button again = UiButton(device, "AgainButton", "У ЛОБІ", 22f, false);
            PlaceTopLeft((RectTransform)again.transform, 180, 328, 120, 24);
            Button leave = UiButton(device, "LeaveButton", "ВИЙТИ", 22f, true);
            PlaceTopLeft((RectTransform)leave.transform, 340, 328, 120, 24);
            TMP_Text wait = TextAt(device, "WaitLabel", "чекаємо хоста…", 180, 314, 280, 8, Palette.Muted, TextAlignmentOptions.Top);

            ResultScreen logic = screen.AddComponent<ResultScreen>();
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("_headline").objectReferenceValue = headline;
            so.FindProperty("_stats").objectReferenceValue = stats;
            so.FindProperty("_stamp").objectReferenceValue = stamp;
            so.FindProperty("_stampWin").objectReferenceValue = HeistSprite("news_stamp_win");
            so.FindProperty("_stampFail").objectReferenceValue = HeistSprite("news_stamp_fail");
            so.FindProperty("_againButton").objectReferenceValue = again;
            so.FindProperty("_leaveButton").objectReferenceValue = leave;
            so.FindProperty("_waitLabel").objectReferenceValue = wait;
            so.ApplyModifiedPropertiesWithoutUndo();
            return screen;
        }
    }
}
