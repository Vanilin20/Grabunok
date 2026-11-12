using System.Collections.Generic;
using Banda.Core;
using Banda.Heist;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Екран Зломщика (перенесено з Godot role_safecracker.gd за поведінкою): диск крутиться пальцем,
    /// кожна поділка — легкий клац, потрібна цифра поточного кроку — сильний поштовх (на телефоні вібрація,
    /// на ПК — золотий спалах довкола диска й гучніший клац). Три цифри фіксуються кнопкою й ідуть хосту однією дією.
    ///
    /// ВІДХИЛЕННЯ від ТЗ/Godot: вертикальний режим і гіроскоп (поворот телефона крутить диск) не перенесені —
    /// екран лишається горизонтальним, диск крутиться лише пальцем/мишею.
    /// </summary>
    public sealed class SafeScreen : RoleScreenBase
    {
        [SerializeField] private Image _dial;
        [SerializeField] private PointerRelay _dialInput;
        [SerializeField] private Texture2D _dialSheet;
        [SerializeField] private int _dialFrame = 112;
        [SerializeField] private Image[] _lamps = System.Array.Empty<Image>();
        [SerializeField] private Sprite _lampOff;
        [SerializeField] private Sprite _lampOn;
        [SerializeField] private Sprite _lampRed;
        [SerializeField] private Image _hitFlash;
        [SerializeField] private TMP_Text _lcd;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Button _lockButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _tick;
        [SerializeField] private AudioClip _hit;
        [SerializeField] private AudioClip _fail;

        private readonly List<int> _entered = new List<int>();
        private Sprite[] _frames = System.Array.Empty<Sprite>();
        private int[] _combo = System.Array.Empty<int>();
        private int _range = 40;
        private float _angle; // радіани, за годинниковою; 0 — цифра 0 угорі
        private int _lastNumber;
        private bool _dragging;
        private float _dragAngle;
        private float _flash;
        private float _failFlash;
        private int _mistakes;
        private bool _open;

        protected override RoleId Role => RoleId.Safecracker;

        protected override void Awake()
        {
            base.Awake();
            _lockButton.onClick.AddListener(OnLock);
            _resetButton.onClick.AddListener(OnReset);
            _dialInput.Down += OnPointerDown;
            _dialInput.Dragged += OnDrag;
            _dialInput.Up += OnPointerUp;
        }

        private void OnDestroy()
        {
            foreach (Sprite s in _frames)
            {
                if (s != null)
                {
                    Destroy(s);
                }
            }
        }

        protected override void OnStatic(RoleStatic data)
        {
            SafecrackerStatic s = (SafecrackerStatic)data;
            _combo = s.Combo;
            _range = s.Range;
            BuildFrames();
            _entered.Clear();
            UpdateLamps();
        }

        private void BuildFrames()
        {
            if (_dialSheet == null || _frames.Length == _range)
            {
                return;
            }

            _frames = new Sprite[_range];
            for (int i = 0; i < _range; i++)
            {
                Rect rect = new Rect(i * _dialFrame, 0, _dialFrame, _dialFrame);
                _frames[i] = Sprite.Create(_dialSheet, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        protected override void OnSnapshot(RoleSnapshot snapshot)
        {
            SafecrackerView view = (SafecrackerView)snapshot.View;
            if (view.Mistakes > _mistakes)
            {
                _failFlash = 0.6f;
                Play(_fail);
                Vibrate();
            }

            _mistakes = view.Mistakes;
            if (view.Open && !_open)
            {
                Play(_hit);
            }

            _open = view.Open;
            _lockButton.interactable = !_open;
            _resetButton.interactable = !_open;
            _status.text = _open ? "СЕЙФ ВІДКРИТО — ТІНЬ МОЖЕ БРАТИ" : _mistakes > 0 ? $"ПОМИЛОК: {_mistakes}" : "КРУТИ ДИСК. ВЛУЧИВ — СПАЛАХ";
            _status.color = _open ? Palette.Safe : _mistakes > 0 ? Palette.Danger : Palette.Text;
            UpdateLamps();
        }

        private void OnPointerDown(PointerEventData eventData)
        {
            if (_open)
            {
                return;
            }

            Vector2 local = Local(eventData);
            if (local.magnitude > _dial.rectTransform.rect.width * 0.75f)
            {
                return;
            }

            _dragging = true;
            _dragAngle = Mathf.Atan2(local.y, local.x);
        }

        private void OnDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            Vector2 local = Local(eventData);
            float a = Mathf.Atan2(local.y, local.x);
            // Unity: Y угору, кут проти годинникової; диск у Godot рахував за годинниковою — знак міняємо.
            _angle -= Mathf.DeltaAngle(_dragAngle * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            _dragAngle = a;
            CheckTicks();
        }

        private void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;
        }

        private Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_dial.rectTransform, e.position, e.pressEventCamera, out Vector2 local);
            return local;
        }

        /// <summary>Цифра проти мітки вгорі (як у Godot: диск за годинниковою — цифри йдуть назад).</summary>
        private int CurrentNumber()
        {
            float step = Mathf.PI * 2f / _range;
            int n = Mathf.RoundToInt(-_angle / step) % _range;
            return n < 0 ? n + _range : n;
        }

        private void CheckTicks()
        {
            int number = CurrentNumber();
            if (number == _lastNumber)
            {
                return;
            }

            _lastNumber = number;
            int step = _entered.Count;
            if (step < _combo.Length && number == _combo[step])
            {
                _flash = 0.25f;
                Play(_hit);
                Vibrate();
            }
            else
            {
                Play(_tick);
            }
        }

        private void OnLock()
        {
            if (_entered.Count >= 3 || _open)
            {
                return;
            }

            _entered.Add(CurrentNumber());
            if (_entered.Count == 3)
            {
                Send(HeistAction.SafeTry(_entered.ToArray()));
                _entered.Clear();
            }

            UpdateLamps();
        }

        private void OnReset()
        {
            _entered.Clear();
            UpdateLamps();
        }

        private void UpdateLamps()
        {
            for (int i = 0; i < _lamps.Length; i++)
            {
                _lamps[i].sprite = _open ? _lampOn : _failFlash > 0f ? _lampRed : i < _entered.Count ? _lampOn : _lampOff;
            }
        }

        private void Update()
        {
            if (!IsShown)
            {
                return;
            }

            _flash = Mathf.Max(_flash - Time.deltaTime, 0f);
            float before = _failFlash;
            _failFlash = Mathf.Max(_failFlash - Time.deltaTime, 0f);
            if (before > 0f && _failFlash <= 0f)
            {
                UpdateLamps();
            }

            if (_frames.Length > 0)
            {
                _dial.sprite = _frames[CurrentNumber()];
            }

            Color glow = _hitFlash.color;
            glow.a = _flash > 0f ? 1f : _failFlash > 0f ? 0.6f : 0f;
            _hitFlash.color = glow;
            _hitFlash.color = new Color(_failFlash > 0f ? Palette.Danger.r : Palette.Lamp.r, _failFlash > 0f ? Palette.Danger.g : Palette.Lamp.g,
                _failFlash > 0f ? Palette.Danger.b : Palette.Lamp.b, glow.a);
            string entered = string.Join(" · ", _entered);
            _lcd.text = _entered.Count == 0 ? CurrentNumber().ToString("00") : $"{entered} · {CurrentNumber():00}";
        }

        private void Play(AudioClip clip)
        {
            if (_audio != null && clip != null)
            {
                _audio.PlayOneShot(clip);
            }
        }

        private static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
