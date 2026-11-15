using System;
using Banda.Heist;
using Banda.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Вікно «Взлом» на екрані ноутбука Хакера: дроти перетягуються пальцем від лівих штекерів до гнізд того ж кольору.
    /// Картинку малює HackBoardPainter у текстуру Point; тексти — TextMeshPro поверх. Компонент нічого не вирішує:
    /// головоломку й результат дає хост (HackerView), а розвʼязок лише надсилається подією <see cref="Submitted"/>.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class HackBoard : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float SolvedCloseDelay = 1.1f;

        private RawImage _image;
        private RectTransform _rect;
        private Texture2D _texture;
        private HackBoardPainter _painter;
        private readonly HackBoardState _state = new HackBoardState { Mode = HackMode.Hidden };
        private TMP_Text _title;
        private TMP_Text _timer;
        private TMP_Text _buttonLabel;
        private TMP_Text _banner;
        private TMP_Text _bannerSub;

        private int _puzzleId = -1;
        private int _seenResult = -1;
        private bool _synced;
        private int _target = -1;
        private float _timeLeft;
        private float _timeLimit = HeistSim.HackTimeLimit;
        private float _lockLeft;
        private float _lockMax = 1f;
        private float _solvedLeft;
        private float _shake;
        private HackResult _lockReason = HackResult.None;
        private int _pointerWire = -1;

        /// <summary>Хакер натиснув «Підключити» — індекси гнізд для кожного дроту.</summary>
        public event Action<int[]> Submitted;
        /// <summary>Хакер закрив вікно хрестиком (без штрафу).</summary>
        public event Action Cancelled;

        public bool IsOpen => _state.Mode != HackMode.Hidden;
        public HackMode Mode => _state.Mode;
        public float LockLeft => _lockLeft;
        public int Wires => _state.Wires;
        /// <summary>Кольори гнізд поточної головоломки (для автоводія й тестів).</summary>
        public int[] Right => _state.Right;
        public int[] SocketOf => _state.SocketOf;
        public int Target => _target;
        public Texture2D Texture => _texture;

        /// <summary>Створює вікно під батьком: розмір і місце — як схема на ноутбуці. Шрифт береться з зразка.</summary>
        public static HackBoard Create(RectTransform parent, Vector2 anchoredPosition, TMP_Text fontSample, int siblingIndex)
        {
            GameObject go = new GameObject("HackBoard", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(siblingIndex);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(HackBoardPainter.W, HackBoardPainter.H);
            HackBoard board = go.AddComponent<HackBoard>();
            board.Build(fontSample);
            go.SetActive(false);
            return board;
        }

        private void Build(TMP_Text fontSample)
        {
            _rect = (RectTransform)transform;
            _basePos = _rect.anchoredPosition;
            _basePosSet = true;
            _image = GetComponent<RawImage>();
            _painter = new HackBoardPainter();
            _texture = new Texture2D(HackBoardPainter.W, HackBoardPainter.H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "HackBoardTexture",
            };
            _image.texture = _texture;
            _image.raycastTarget = true;

            _title = MakeText("Title", fontSample, new Rect(8f, 2f, 250f, 12f), TextAlignmentOptions.Left, 8f);
            _timer = MakeText("Timer", fontSample, new Rect(236f, 2f, 80f, 12f), TextAlignmentOptions.Right, 8f);
            _buttonLabel = MakeText("ButtonLabel", fontSample, new Rect(
                HackBoardPainter.SubmitRect.x, HackBoardPainter.SubmitRect.y + 7f, HackBoardPainter.SubmitRect.width, 10f), TextAlignmentOptions.Center, 8f);
            _banner = MakeText("Banner", fontSample, new Rect(40f, 38f, HackBoardPainter.W - 80f, 12f), TextAlignmentOptions.Center, 8f);
            _bannerSub = MakeText("BannerSub", fontSample, new Rect(40f, 49f, HackBoardPainter.W - 80f, 10f), TextAlignmentOptions.Center, 8f);
        }

        private TMP_Text MakeText(string objectName, TMP_Text sample, Rect r, TextAlignmentOptions align, float size)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = new Vector2(r.width, r.height);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.font = sample.font;
            t.fontSharedMaterial = sample.fontSharedMaterial;
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            t.text = string.Empty;
            return t;
        }

        // ------------------------------------------------------------------ стан від хоста

        /// <summary>Узгодити вікно зі знімком хоста: нова головоломка, результат, лишок часу й блокування.</summary>
        public void Sync(HackerView view)
        {
            _lockLeft = view.HackLockLeft;
            _lockMax = Mathf.Max(_lockMax, _lockLeft);
            if (!_synced)
            {
                // Перший знімок після (пере)підключення: старий результат не показуємо.
                _synced = true;
                _seenResult = view.HackResultSeq;
            }

            if (view.HackActive)
            {
                if (_state.Mode != HackMode.Puzzle || _puzzleId != view.HackId)
                {
                    OpenPuzzle(view);
                }
                else
                {
                    _timeLeft = view.HackTimeLeft;
                }
            }

            if (view.HackResultSeq != _seenResult)
            {
                _seenResult = view.HackResultSeq;
                if (_state.Mode == HackMode.Puzzle && !view.HackActive)
                {
                    ShowResult(view.HackLastResult);
                }
            }
            else if (!view.HackActive && _state.Mode == HackMode.Puzzle)
            {
                Close(); // скасовано, або хост не прийняв
            }

            if (_state.Mode == HackMode.Locked && _lockLeft <= 0f)
            {
                Close();
            }
        }

        private void OpenPuzzle(HackerView view)
        {
            _puzzleId = view.HackId;
            _target = view.HackTarget;
            int n = view.HackRight.Length;
            _state.Mode = HackMode.Puzzle;
            _state.Wires = n;
            _state.Right = (int[])view.HackRight.Clone();
            _state.SocketOf = new int[n];
            for (int i = 0; i < n; i++)
            {
                _state.SocketOf[i] = -1;
            }

            _state.Dragging = -1;
            _state.Busy = false;
            _state.CanSubmit = false;
            _state.Failed = false;
            _state.Flash = 0f;
            _timeLeft = view.HackTimeLeft;
            _timeLimit = HeistSim.HackTimeLimit;
            _pointerWire = -1;
            gameObject.SetActive(true);
            RefreshTexts();
        }

        private void ShowResult(HackResult result)
        {
            _state.Flash = 1f;
            _state.Dragging = -1;
            _pointerWire = -1;
            if (result == HackResult.Solved)
            {
                _state.Mode = HackMode.Solved;
                _solvedLeft = SolvedCloseDelay;
            }
            else
            {
                _state.Mode = HackMode.Locked;
                Deny();
                _state.Failed = result == HackResult.Wrong;
                _lockReason = result;
                _shake = result == HackResult.Wrong ? 1f : 0.4f;
            }

            RefreshTexts();
        }

        /// <summary>Хакер торкнувся камери під час блокування: показуємо, скільки чекати (на хост нічого не шлемо).</summary>
        public void ShowLocked()
        {
            _state.Mode = HackMode.Locked;
            _state.Failed = false;
            _state.Flash = 0.3f;
            _lockReason = HackResult.None;
            _shake = 0.25f;
            if (_state.Right.Length == 0)
            {
                _state.Wires = HeistSim.HackWiresCamera;
                _state.Right = new[] { 1, 2, 3, 0 };
                _state.SocketOf = new[] { -1, -1, -1, -1 };
            }

            gameObject.SetActive(true);
            RefreshTexts();
        }

        /// <summary>Забути, що вже бачили: наступний знімок вважається першим (нова справа, перепідключення).</summary>
        public void ResetSync()
        {
            _synced = false;
            _puzzleId = -1;
            _lockLeft = 0f;
        }

        public void Close()
        {
            _state.Mode = HackMode.Hidden;
            _state.Dragging = -1;
            _pointerWire = -1;
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ тексти

        private static string Seconds(float s) => Mathf.CeilToInt(Mathf.Max(0f, s)) + "С";

        private void RefreshTexts()
        {
            string what = _target == -1 ? "ДВЕРІ" : "КАМЕРА " + (_target + 1);
            _title.text = "ВЗЛОМ · " + what;
            _title.color = new Color32(194, 246, 255, 255);
            bool puzzle = _state.Mode == HackMode.Puzzle;
            _timer.text = puzzle ? Seconds(_timeLeft) : string.Empty;
            Color32 danger = new Color32(255, 150, 130, 255);
            _timer.color = _timeLeft / _timeLimit < 0.3f ? danger : new Color32(194, 246, 255, 255);

            bool ready = puzzle && _state.CanSubmit && !_state.Busy;
            bool locked = _state.Mode == HackMode.Locked;
            bool solved = _state.Mode == HackMode.Solved;
            _buttonLabel.gameObject.SetActive(puzzle || locked || solved);
            _buttonLabel.text = solved ? "ГОТОВО" : locked ? "БЛОК " + Seconds(_lockLeft) : _state.Busy ? "ПЕРЕВІРКА…" : _state.CanSubmit ? "ПІДКЛЮЧИТИ" : "СПОЛУЧИ ВСІ ДРОТИ";
            _buttonLabel.color = solved ? new Color32(7, 6, 13, 255) : locked ? new Color32(255, 150, 130, 255) : ready ? new Color32(7, 6, 13, 255) : new Color32(160, 178, 194, 255);
            // На світлій кнопці чорний текст, на темній неактивній — приглушений: різниця видна за пів секунди.
            _buttonLabel.rectTransform.anchoredPosition = new Vector2(HackBoardPainter.SubmitRect.x, -(HackBoardPainter.SubmitRect.y + 7f + (_state.Pressed && ready ? 1f : 0f)));

            bool banner = _state.Mode == HackMode.Solved || _state.Mode == HackMode.Locked;
            _banner.gameObject.SetActive(banner);
            _bannerSub.gameObject.SetActive(banner);
            if (_state.Mode == HackMode.Solved)
            {
                _banner.text = "ДОСТУП ОТРИМАНО";
                _banner.color = new Color32(92, 240, 138, 255);
                _bannerSub.text = _target == -1 ? "ДВЕРІ ПЕРЕМКНЕНО" : "КАМЕРА ВИМКНЕНА " + Mathf.RoundToInt(HeistSim.CameraOffTime) + "С";
                _bannerSub.color = new Color32(190, 255, 205, 255);
            }
            else if (_state.Mode == HackMode.Locked)
            {
                _banner.text = _lockReason == HackResult.Wrong ? "ПОМИЛКА" : _lockReason == HackResult.TimedOut ? "ЧАС ВИЙШОВ" : "ЗАБЛОКОВАНО";
                _banner.color = new Color32(255, 150, 130, 255);
                _bannerSub.text = _lockReason == HackResult.Wrong ? "ТРИВОГА +" + Mathf.RoundToInt(HeistSim.AlarmHackWrong) + "%" : _lockReason == HackResult.TimedOut ? "ДРОТИ ЗАЛИШИЛИСЬ" : "ПЕРЕЗАВАНТАЖЕННЯ";
                _bannerSub.color = new Color32(247, 201, 90, 255);
            }
        }

        // ------------------------------------------------------------------ кадр

        private void Update()
        {
            if (_state.Mode == HackMode.Hidden)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _state.Clock += dt;
            _lockLeft = Mathf.Max(0f, _lockLeft - dt);
            if (_state.Mode == HackMode.Puzzle)
            {
                _timeLeft = Mathf.Max(0f, _timeLeft - dt);
                _state.TimeFrac = _timeLeft / _timeLimit;
            }
            else if (_state.Mode == HackMode.Solved)
            {
                _solvedLeft -= dt;
                if (_solvedLeft <= 0f)
                {
                    Close();
                    return;
                }
            }
            else if (_state.Mode == HackMode.Locked && _lockLeft <= 0f)
            {
                Close();
                return;
            }

            _state.Spark = Mathf.Max(0f, _state.Spark - dt * 2.6f);
            _state.LockFrac = Mathf.Clamp01(_lockLeft / Mathf.Max(0.01f, _lockMax));
            _state.Flash = Mathf.Max(0f, _state.Flash - dt * 1.4f);
            _shake = Mathf.Max(0f, _shake - dt * 2.2f);
            RefreshTexts();
            _painter.Paint(_state);
            _texture.SetPixels32(_painter.Pixels);
            _texture.Apply(false);
            ApplyShake();
        }

        private Vector2 _basePos;
        private bool _basePosSet;

        private void ApplyShake()
        {
            if (!_basePosSet)
            {
                _basePos = _rect.anchoredPosition;
                _basePosSet = true;
            }

            Vector2 offset = _shake > 0.02f
                ? new Vector2(Mathf.Round(Mathf.Sin(_state.Clock * 90f) * 3f * _shake), Mathf.Round(Mathf.Cos(_state.Clock * 71f) * 2f * _shake))
                : Vector2.zero;
            _rect.anchoredPosition = _basePos + offset;
        }

        // ------------------------------------------------------------------ дотики

        private Vector2 ToBoard(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, e.pressEventCamera, out Vector2 local);
            Rect r = _rect.rect;
            return new Vector2(local.x - r.xMin, r.yMax - local.y);
        }

        public void OnPointerDown(PointerEventData eventData) => PointerDown(ToBoard(eventData));

        public void OnDrag(PointerEventData eventData) => PointerMove(ToBoard(eventData));

        public void OnPointerUp(PointerEventData eventData) => PointerUp(ToBoard(eventData));

        /// <summary>Натиснення в координатах вікна (піксель базової роздільності, Y униз). Публічно — для автоводія й тестів.</summary>
        public void PointerDown(Vector2 p)
        {
            if (_state.Mode == HackMode.Hidden)
            {
                return;
            }

            if (HackBoardPainter.CancelRect.Contains(Vector2Int.RoundToInt(p)))
            {
                CloseByUser();
                return;
            }

            if (_state.Mode != HackMode.Puzzle || _state.Busy)
            {
                return;
            }

            if (_state.CanSubmit && HackBoardPainter.SubmitRect.Contains(Vector2Int.RoundToInt(p)))
            {
                _state.Pressed = true;
                return;
            }

            int wire = PickWire(p);
            if (wire >= 0)
            {
                _state.SocketOf[wire] = -1; // вийнятий дріт відʼєднується від гнізда
                _state.Dragging = wire;
                _state.DragPos = p;
                _pointerWire = wire;
                Click();
                UpdateReady();
            }
        }

        public void PointerMove(Vector2 p)
        {
            if (_state.Dragging >= 0)
            {
                _state.DragPos = p;
            }
        }

        public void PointerUp(Vector2 p)
        {
            if (_state.Pressed)
            {
                _state.Pressed = false;
                if (_state.CanSubmit && !_state.Busy && HackBoardPainter.SubmitRect.Contains(Vector2Int.RoundToInt(p)))
                {
                    Submit();
                }

                return;
            }

            if (_state.Dragging < 0)
            {
                return;
            }

            int wire = _state.Dragging;
            _state.Dragging = -1;
            _pointerWire = -1;
            int slot = PickSocket(p);
            if (slot >= 0)
            {
                // Гніздо вже зайняте іншим дротом — той дріт повертається (стає вільним).
                for (int i = 0; i < _state.Wires; i++)
                {
                    if (_state.SocketOf[i] == slot)
                    {
                        _state.SocketOf[i] = -1;
                    }
                }

                _state.SocketOf[wire] = slot;
                _state.SparkPos = HackBoardPainter.RightEnd(slot, _state.Wires);
                _state.SparkColor = _state.Right.Length > slot ? _state.Right[slot] : wire;
                _state.Spark = 1f;
                Click();
            }

            UpdateReady();
        }

        private void CloseByUser()
        {
            bool wasPuzzle = _state.Mode == HackMode.Puzzle;
            Close();
            if (wasPuzzle)
            {
                Cancelled?.Invoke();
            }
        }

        private int PickWire(Vector2 p)
        {
            int best = -1;
            float bestD = HackBoardPainter.PickRadius;
            for (int i = 0; i < _state.Wires; i++)
            {
                float d = Vector2.Distance(p, HackBoardPainter.LeftStart(i, _state.Wires) - new Vector2(6f, 0f));
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            // Вже вставлений дріт можна підхопити й за гніздо.
            for (int i = 0; i < _state.Wires && best < 0; i++)
            {
                if (_state.SocketOf[i] >= 0 && Vector2.Distance(p, HackBoardPainter.RightEnd(_state.SocketOf[i], _state.Wires)) < 12f)
                {
                    best = i;
                }
            }

            return best;
        }

        private int PickSocket(Vector2 p)
        {
            int best = -1;
            float bestD = HackBoardPainter.PickRadius;
            for (int s = 0; s < _state.Wires; s++)
            {
                float d = Vector2.Distance(p, HackBoardPainter.RightEnd(s, _state.Wires));
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }

            return best;
        }

        private static void Click()
        {
            if (UiSound.Instance != null)
            {
                UiSound.Instance.PlayClick();
            }
        }

        private static void Deny()
        {
            if (UiSound.Instance != null)
            {
                UiSound.Instance.PlayDeny();
            }
        }

        private void UpdateReady()
        {
            bool all = _state.Wires > 0;
            for (int i = 0; i < _state.Wires; i++)
            {
                all &= _state.SocketOf[i] >= 0;
            }

            _state.CanSubmit = all;
        }

        private void Submit()
        {
            _state.Busy = true;
            Click();
            Submitted?.Invoke((int[])_state.SocketOf.Clone());
        }

        // ------------------------------------------------------------------ для автоводія й тестів

        /// <summary>Провести дріт від лівого штекера wire до гнізда slot, як пальцем (3 кроки).</summary>
        public void DebugConnect(int wire, int slot)
        {
            PointerDown(HackBoardPainter.LeftStart(wire, _state.Wires) - new Vector2(6f, 0f));
            PointerMove(Vector2.Lerp(HackBoardPainter.LeftStart(wire, _state.Wires), HackBoardPainter.RightEnd(slot, _state.Wires), 0.5f));
            PointerUp(HackBoardPainter.RightEnd(slot, _state.Wires));
        }

        /// <summary>Узяти дріт і потягнути на частку шляху до гнізда, не відпускаючи.</summary>
        public void DebugDragHalf(int wire, int slot, float fraction)
        {
            PointerDown(HackBoardPainter.LeftStart(wire, _state.Wires) - new Vector2(6f, 0f));
            PointerMove(Vector2.Lerp(HackBoardPainter.LeftStart(wire, _state.Wires), HackBoardPainter.RightEnd(slot, _state.Wires), fraction) + new Vector2(0f, -10f));
        }

        /// <summary>Відпустити дріт, який тягнуть, над гніздом slot.</summary>
        public void DebugDrop(int slot) => PointerUp(HackBoardPainter.RightEnd(slot, _state.Wires));

        public void DebugPressSubmit()
        {
            Vector2 c = new Vector2(HackBoardPainter.SubmitRect.center.x, HackBoardPainter.SubmitRect.center.y);
            PointerDown(c);
            PointerUp(c);
        }
    }
}
