using System.Collections.Generic;
using Banda.Core;
using Banda.Heist;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Світ справи для Тіні: магазин згори, світло, персонажі. Показує ТІЛЬКИ те, що прийшло в ShadowView
    /// (охоронець — лише в колі видимості; двері й сейф — останній побачений стан).
    /// Клієнт не рахує позицію: шле напрям, отримує позицію від хоста й згладжує (як у Godot, SMOOTHING 14).
    /// Поки активна інша роль («гаряче місце»), світ вимкнений — Хакер і Зломщик працюють у своїх пристроях.
    /// </summary>
    public sealed class HeistWorldView : MonoBehaviour
    {
        private const float Smoothing = 14f;
        private const float CameraZoomSize = 90f; // ×2 від базових 360 px висоти

        [SerializeField] private Transform _levelRoot;
        [SerializeField] private CharacterSpriteAnimator _shadow;
        [SerializeField] private CharacterSpriteAnimator _guard;
        [SerializeField] private Light2D _guardFlashlight;
        [SerializeField] private SpriteRenderer _door;
        [SerializeField] private Sprite _doorClosed;
        [SerializeField] private Sprite _doorOpen;
        [SerializeField] private SpriteRenderer _safe;
        [SerializeField] private Sprite _safeClosed;
        [SerializeField] private Sprite _safeOpen;
        [SerializeField] private Light2D _globalLight;
        [SerializeField] private Color _nightColor = new Color(0.34f, 0.29f, 0.47f);
        [SerializeField] private float _nightIntensity = 0.35f;
        [SerializeField] private GameObject _menuBackdrop;
        [Tooltip("Межі арту рівня в локальних координатах (store_base з полем довкола).")]
        [SerializeField] private Vector2 _artMin = new Vector2(-48f, -256f);
        [SerializeField] private Vector2 _artMax = new Vector2(400f, 48f);

        private ITransport _transport;
        private Camera _camera;
        private Vector3 _cameraHome;
        private float _cameraHomeSize;
        private Color _menuLightColor;
        private float _menuLightIntensity;
        private Vector3 _shadowTarget;
        private Vector3 _guardTarget;
        private bool _hasShadow;
        private bool _active;

        private void Awake()
        {
            _camera = Camera.main;
            gameObject.SetActive(true);
            _levelRoot.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (HeistDirector.Instance != null)
            {
                HeistDirector.Instance.HeistStarted += OnHeistStarted;
                HeistDirector.Instance.HeistStopped += OnHeistStopped;
            }
        }

        private void OnDisable()
        {
            if (HeistDirector.Instance != null)
            {
                HeistDirector.Instance.HeistStarted -= OnHeistStarted;
                HeistDirector.Instance.HeistStopped -= OnHeistStopped;
            }

            Detach();
        }

        private void OnHeistStarted(ITransport transport)
        {
            Detach();
            _transport = transport;
            _transport.SnapshotReceived += OnSnapshot;
            _transport.LocalRoleChanged += OnRoleChanged;
            _hasShadow = false;
            OnRoleChanged(_transport.LocalRole);
        }

        private void OnHeistStopped()
        {
            Detach();
        }

        private void Detach()
        {
            if (_transport != null)
            {
                _transport.SnapshotReceived -= OnSnapshot;
                _transport.LocalRoleChanged -= OnRoleChanged;
                _transport = null;
            }

            SetActive(false);
        }

        private void OnRoleChanged(RoleId role)
        {
            SetActive(role == RoleId.Shadow);
        }

        /// <summary>Увімкнути світ справи: камера переїжджає сюди, ніч густішає, фон меню засинає.</summary>
        private void SetActive(bool active)
        {
            if (_active == active)
            {
                return;
            }

            _active = active;
            _levelRoot.gameObject.SetActive(active);
            if (_menuBackdrop != null)
            {
                _menuBackdrop.SetActive(!active);
            }

            if (_camera == null)
            {
                return;
            }

            if (active)
            {
                _cameraHome = _camera.transform.position;
                _cameraHomeSize = _camera.orthographicSize;
                _camera.orthographicSize = CameraZoomSize;
                if (_globalLight != null)
                {
                    _menuLightColor = _globalLight.color;
                    _menuLightIntensity = _globalLight.intensity;
                    _globalLight.color = _nightColor;
                    _globalLight.intensity = _nightIntensity;
                }
            }
            else
            {
                _camera.transform.position = _cameraHome;
                if (_cameraHomeSize > 0f)
                {
                    _camera.orthographicSize = _cameraHomeSize;
                }

                if (_globalLight != null && _menuLightIntensity > 0f)
                {
                    _globalLight.color = _menuLightColor;
                    _globalLight.intensity = _menuLightIntensity;
                }
            }
        }

        private void OnSnapshot(RoleSnapshot snapshot)
        {
            if (!(snapshot.View is ShadowView view))
            {
                return;
            }

            _shadowTarget = ToWorld(view.Position);
            if (!_hasShadow)
            {
                _shadow.transform.localPosition = _shadowTarget;
                _hasShadow = true;
            }

            _shadow.SetHidden(view.Hidden);
            _guard.gameObject.SetActive(view.GuardVisible);
            if (view.GuardVisible)
            {
                Vector3 target = ToWorld(view.Guard);
                if (!_guard.gameObject.activeSelf || (_guard.transform.localPosition - target).sqrMagnitude > 400f)
                {
                    _guard.transform.localPosition = target;
                }

                _guardTarget = target;
                _guard.Face(view.GuardFacing);
                // Ліхтарик Light2D світить уздовж локальної осі Y: поворот від напрямку погляду (Y униз у симуляції).
                float degrees = -view.GuardFacing * Mathf.Rad2Deg - 90f;
                _guardFlashlight.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            }

            if (view.DoorVisible)
            {
                _door.sprite = view.DoorOpen ? _doorOpen : _doorClosed;
            }

            if (view.SafeVisible)
            {
                _safe.sprite = view.SafeOpen ? _safeOpen : _safeClosed;
            }
        }

        private void LateUpdate()
        {
            if (!_active || !_hasShadow)
            {
                return;
            }

            float k = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
            Vector3 before = _shadow.transform.localPosition;
            Vector3 next = Vector3.Lerp(before, _shadowTarget, k);
            _shadow.transform.localPosition = next;
            _shadow.SetVelocity((next - before) / Mathf.Max(Time.deltaTime, 1e-4f));
            if (_guard.gameObject.activeSelf)
            {
                Vector3 g0 = _guard.transform.localPosition;
                Vector3 g1 = Vector3.Lerp(g0, _guardTarget, k);
                _guard.transform.localPosition = g1;
                _guard.SetVelocity((g1 - g0) / Mathf.Max(Time.deltaTime, 1e-4f));
            }

            if (_camera != null)
            {
                // Камера не виїжджає за межі арту рівня — у кадрі ніколи немає порожнечі.
                Vector3 local = _shadow.transform.localPosition;
                float halfH = _camera.orthographicSize;
                float halfW = halfH * _camera.aspect;
                local.x = ClampAxis(local.x, _artMin.x + halfW, _artMax.x - halfW);
                local.y = ClampAxis(local.y, _artMin.y + halfH, _artMax.y - halfH);
                Vector3 world = _levelRoot.TransformPoint(local);
                // Ціле значення пікселя на екрані: при ×2 крок камери — пів пікселя світу.
                _camera.transform.position = new Vector3(Mathf.Round(world.x * 2f) / 2f, Mathf.Round(world.y * 2f) / 2f, _cameraHome.z);
            }
        }

        /// <summary>Якщо арт вужчий за кадр — центр між межами, інакше обмеження.</summary>
        private static float ClampAxis(float value, float min, float max)
        {
            return min > max ? (min + max) / 2f : Mathf.Clamp(value, min, max);
        }

        /// <summary>Координати рівня (Y униз, від верхнього лівого кута) → локальні координати світу справи.</summary>
        public static Vector3 ToWorld(Vec2 p) => new Vector3(p.X, -p.Y, 0f);

        public static List<Vector3> ToWorld(IEnumerable<Vec2> points)
        {
            List<Vector3> result = new List<Vector3>();
            foreach (Vec2 p in points)
            {
                result.Add(ToWorld(p));
            }

            return result;
        }
    }
}
