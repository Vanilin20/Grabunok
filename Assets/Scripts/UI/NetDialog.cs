using System.Collections;
using System.Collections.Generic;
using Banda.Core;
using Banda.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>
    /// Діалог мережі поверх будь-якого екрана: вибір «де граємо», пошук і підключення (з «Скасувати»), обрив і повернення,
    /// хост вийшов, помилка сервісу. Вигляд — ті самі панель, шрифт і кнопки, що в меню (клони шаблонів), без окремих «технічних» елементів.
    /// Стан бере з GameState.Net; сам нічого про мережу не знає.
    /// </summary>
    public sealed class NetDialog : MonoBehaviour
    {
        private const float PanelWidth = 300f;
        private const float ButtonHeight = 26f;

        public static NetDialog Instance { get; private set; }

        [SerializeField] private MainMenuScreen _menu;

        private GameObject _root;
        private Image _panel;
        private TMP_Text _title;
        private TMP_Text _body;
        private RectTransform _buttons;
        private Coroutine _animation;
        private bool _choiceOpen;
        private string _baseBody = string.Empty;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (GameState.Instance != null)
            {
                GameState.Instance.NetChanged += OnNetChanged;
            }

            OnNetChanged();
        }

        private void OnDisable()
        {
            if (GameState.Instance != null)
            {
                GameState.Instance.NetChanged -= OnNetChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------ показ

        /// <summary>Вибір кімнати: поруч, через інтернет чи локально на цьому пристрої («гаряче місце»).</summary>
        public void ShowHostChoice()
        {
            _choiceOpen = true;
            Build("ДЕ ГРАЄМО?", "Обери, як банда збереться.", false, new[]
            {
                ("ПОРУЧ · WI-FI", (System.Action)(() => Host(RoomKind.Lan)), true),
                ("ЧЕРЕЗ ІНТЕРНЕТ", () => Host(RoomKind.Relay), false),
                ("НА ЦЬОМУ ПРИСТРОЇ", HostLocal, false),
                ("НАЗАД", Close, false),
            });
        }

        private void Host(RoomKind kind)
        {
            _choiceOpen = false;
            Close();
            GameState.Instance?.HostOnlineRoom(kind);
        }

        private void HostLocal()
        {
            _choiceOpen = false;
            Close();
            GameState.Instance?.HostRoom();
        }

        private void Close()
        {
            _choiceOpen = false;
            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }

            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void OnNetChanged()
        {
            GameState state = GameState.Instance;
            if (state == null)
            {
                return;
            }

            switch (state.Net)
            {
                case NetStatus.Searching:
                    Build("ШУКАЮ КІМНАТУ", state.NetMessage, true, new[] { ("СКАСУВАТИ", (System.Action)CancelBusy, false) });
                    break;
                case NetStatus.Connecting:
                    Build("ПІДКЛЮЧАЮСЬ", state.NetMessage, true, new[] { ("СКАСУВАТИ", (System.Action)CancelBusy, false) });
                    break;
                case NetStatus.Lost:
                    Build("ЗВ'ЯЗОК ПРОПАВ", state.NetMessage, true, new[] { ("ВИЙТИ В МЕНЮ", (System.Action)Leave, false) });
                    break;
                case NetStatus.Ended:
                    Build("КІМНАТУ ЗАКРИТО", state.NetMessage, false, new[] { ("У МЕНЮ", (System.Action)Leave, true) });
                    break;
                case NetStatus.Failed:
                    Build("НЕ ВИЙШЛО", state.NetMessage, false, new[] { ("ДОБРЕ", (System.Action)Dismiss, true) });
                    break;
                default:
                    if (!_choiceOpen)
                    {
                        Close();
                    }

                    break;
            }
        }

        private static void CancelBusy() => GameState.Instance?.CancelBusy();

        private static void Leave() => GameState.Instance?.LeaveRoom();

        private static void Dismiss() => GameState.Instance?.DismissNetMessage();

        // ------------------------------------------------------------------ побудова

        private void EnsureBuilt()
        {
            if (_root != null)
            {
                return;
            }

            ScreenRouter router = ScreenRouter.Instance;
            _root = new GameObject("NetDialog", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            RectTransform rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100; // поверх екранів і сповіщень

            // Службова підкладка, що гасить фон і не пускає кліки під діалог.
            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(_root.transform, false);
            RectTransform dimRect = (RectTransform)dim.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 0.8f);

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(_root.transform, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, 100f);
            _panel = panel.GetComponent<Image>();
            _panel.sprite = router != null ? router.PanelSprite : null;
            _panel.type = Image.Type.Sliced;
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = MakeText("Title", panel.transform, 10f, router != null ? router.LabelColor : Palette.Lamp, TextAlignmentOptions.Center);
            _body = MakeText("Body", panel.transform, 8f, Palette.Steel2, TextAlignmentOptions.Center);
            GameObject buttons = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttons.transform.SetParent(panel.transform, false);
            VerticalLayoutGroup buttonLayout = buttons.GetComponent<VerticalLayoutGroup>();
            buttonLayout.spacing = 6f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;
            buttonLayout.padding = new RectOffset(0, 0, 6, 0);
            _buttons = (RectTransform)buttons.transform;
        }

        private TMP_Text MakeText(string name, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            ScreenRouter router = ScreenRouter.Instance;
            if (router != null && router.Font != null)
            {
                text.font = router.Font;
            }

            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = 6f;
            text.raycastTarget = false;
            return text;
        }

        private void Build(string title, string body, bool animated, (string label, System.Action action, bool primary)[] buttons)
        {
            EnsureBuilt();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _title.text = title;
            _baseBody = body ?? string.Empty;
            _body.text = _baseBody;
            _body.gameObject.SetActive(_baseBody.Length > 0);
            for (int i = _buttons.childCount - 1; i >= 0; i--)
            {
                Destroy(_buttons.GetChild(i).gameObject);
            }

            Button template = _menu != null ? _menu.ButtonTemplate : null;
            foreach ((string label, System.Action action, bool primary) in buttons)
            {
                if (template == null)
                {
                    break;
                }

                Button button = Instantiate(template, _buttons);
                button.gameObject.name = "Btn_" + label;
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();
                System.Action captured = action;
                button.onClick.AddListener(() => captured());
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                text.text = label;
                LayoutElement element = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
                element.minHeight = ButtonHeight;
                element.preferredHeight = ButtonHeight;
                button.interactable = true;
            }

            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }

            if (animated)
            {
                _animation = StartCoroutine(Animate());
            }
            else
            {
                _panel.color = Color.white;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_panel.transform);
        }

        /// <summary>Пошук «дихає»: рамка панелі пульсує, до тексту додаються крапки. Статичний діалог без руху виглядав би завислим.</summary>
        private IEnumerator Animate()
        {
            float t = 0f;
            int dots = 0;
            float dotTimer = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                dotTimer += Time.unscaledDeltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
                _panel.color = Color.Lerp(new Color(0.75f, 0.85f, 1f, 1f), Color.white, pulse);
                if (dotTimer > 0.4f)
                {
                    dotTimer = 0f;
                    dots = (dots + 1) % 4;
                    _body.text = _baseBody.TrimEnd('…', '.') + new string('.', dots);
                }

                yield return null;
            }
        }
    }
}
