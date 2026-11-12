using System.Collections.Generic;
using Banda.Core;
using Banda.Heist;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Екран Хакера: схема поверху на ноутбуці, камери й двері — тапом (ТЗ). Охоронець — червона точка з променем погляду.
    /// Стрічка з камери шумить; коли камеру вимкнено — «ВИМКНЕНО» і зворотний відлік.
    /// Хакер не отримує позиції Тіні й комбінації — їх немає в HackerView (перевірено тестами).
    /// </summary>
    public sealed class HackerScreen : RoleScreenBase
    {
        [SerializeField] private RectTransform _schemeRoot;
        [SerializeField] private Button _cameraTemplate;
        [SerializeField] private Button _doorButton;
        [SerializeField] private Image _doorIcon;
        [SerializeField] private Sprite _doorOpenSprite;
        [SerializeField] private Sprite _doorClosedSprite;
        [SerializeField] private Sprite _camOnSprite;
        [SerializeField] private Sprite _camOffSprite;
        [SerializeField] private RectTransform _guard;
        [SerializeField] private RectTransform _guardGaze;
        [SerializeField] private Image _feedNoise;
        [SerializeField] private Sprite[] _noiseFrames = System.Array.Empty<Sprite>();
        [SerializeField] private TMP_Text _feedStatus;
        [SerializeField] private TMP_Text _alertLabel;

        private readonly List<Button> _cameras = new List<Button>();
        private readonly List<TMP_Text> _cameraTimers = new List<TMP_Text>();
        private Vector2 _guardTarget;
        private float _noiseTime;

        protected override RoleId Role => RoleId.Hacker;

        protected override void Awake()
        {
            base.Awake();
            _cameraTemplate.gameObject.SetActive(false);
            _doorButton.onClick.AddListener(() => Send(HeistAction.DoorToggle()));
        }

        protected override void OnStatic(RoleStatic data)
        {
            HackerStatic scheme = (HackerStatic)data;
            foreach (Button b in _cameras)
            {
                Destroy(b.gameObject);
            }

            _cameras.Clear();
            _cameraTimers.Clear();
            for (int i = 0; i < scheme.Cameras.Count; i++)
            {
                Button cam = Instantiate(_cameraTemplate, _schemeRoot);
                cam.gameObject.SetActive(true);
                cam.name = "Camera_" + i;
                ((RectTransform)cam.transform).anchoredPosition = ToScheme(scheme.Cameras[i]);
                int index = i;
                cam.onClick.AddListener(() => Send(HeistAction.CameraOff(index)));
                _cameras.Add(cam);
                _cameraTimers.Add(cam.GetComponentInChildren<TMP_Text>(true));
            }

            ((RectTransform)_doorButton.transform).anchoredPosition = ToScheme(scheme.Door.Center);
        }

        protected override void OnSnapshot(RoleSnapshot snapshot)
        {
            HackerView view = (HackerView)snapshot.View;
            bool anyOff = false;
            for (int i = 0; i < _cameras.Count && i < view.CamerasOff.Count; i++)
            {
                bool off = view.CamerasOff[i] > 0f;
                anyOff |= off;
                Image icon = (Image)_cameras[i].targetGraphic;
                icon.sprite = off ? _camOffSprite : _camOnSprite;
                if (_cameraTimers[i] != null)
                {
                    _cameraTimers[i].text = off ? $"{Mathf.CeilToInt(view.CamerasOff[i])}С" : string.Empty;
                }
            }

            _doorIcon.sprite = view.DoorOpen ? _doorOpenSprite : _doorClosedSprite;
            _guardTarget = ToScheme(view.Guard);
            _guardGaze.localRotation = Quaternion.Euler(0f, 0f, -view.GuardFacing * Mathf.Rad2Deg);
            _feedStatus.text = anyOff ? "ВИМКНЕНО" : "НАЖИВО";
            _feedStatus.color = anyOff ? Palette.Danger : Palette.Safe;
            _alertLabel.gameObject.SetActive(view.Alert);
        }

        private void Update()
        {
            if (!IsShown)
            {
                return;
            }

            _guard.anchoredPosition = Vector2.Lerp(_guard.anchoredPosition, _guardTarget, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (_noiseFrames.Length > 0)
            {
                _noiseTime += Time.deltaTime * 12f;
                _feedNoise.sprite = _noiseFrames[Mathf.FloorToInt(_noiseTime) % _noiseFrames.Length];
            }
        }

        /// <summary>Координати рівня → позиція на схемі (1 px рівня = 1 px схеми, Y униз).</summary>
        private static Vector2 ToScheme(Vec2 p) => new Vector2(Mathf.Round(p.X) + 1f, -Mathf.Round(p.Y) - 1f);
    }
}
