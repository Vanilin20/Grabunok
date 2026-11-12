using Banda.Core;
using Banda.Heist;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Керування Тінню (ТЗ: «джойстик ліворуч, кнопки "взяти" і "сховатися" праворуч»).
    /// Шле хосту лише напрям 15 разів на секунду (як у Godot, INPUT_SEND_RATE). На ПК — WASD/стрілки, E, Пробіл.
    /// </summary>
    public sealed class ShadowControls : RoleScreenBase
    {
        private const float SendRate = 15f;

        [SerializeField] private RectTransform _stickArea;
        [SerializeField] private PointerRelay _stickInput;
        [SerializeField] private RectTransform _knob;
        [SerializeField] private float _stickRadius = 24f;
        [SerializeField] private Button _hideButton;
        [SerializeField] private Button _takeButton;

        private Vector2 _stick;
        private Vector2 _lastSent = new Vector2(9f, 9f);
        private float _sendTimer;
        private int _pointer = -1;

        protected override RoleId Role => RoleId.Shadow;

        protected override void Awake()
        {
            base.Awake();
            _hideButton.onClick.AddListener(() => Send(HeistAction.Hide()));
            _takeButton.onClick.AddListener(() => Send(HeistAction.Take()));
            _stickInput.Down += OnPointerDown;
            _stickInput.Dragged += OnDrag;
            _stickInput.Up += OnPointerUp;
        }

        protected override void OnStatic(RoleStatic data)
        {
        }

        protected override void OnSnapshot(RoleSnapshot snapshot)
        {
            ShadowView view = (ShadowView)snapshot.View;
            // Вимкнені кнопки пояснюють себе: «взяти» горить тільки біля відчиненого сейфа.
            _hideButton.interactable = view.CanHide || view.Hidden;
            _takeButton.interactable = view.CanTake;
        }

        protected override void OnShown(bool shown)
        {
            _stick = Vector2.zero;
            _pointer = -1;
            UpdateKnob();
        }

        private void OnPointerDown(PointerEventData eventData)
        {
            if (_pointer != -1 || !RectTransformUtility.RectangleContainsScreenPoint(_stickArea, eventData.position, eventData.pressEventCamera))
            {
                return;
            }

            _pointer = eventData.pointerId;
            OnDrag(eventData);
        }

        private void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointer)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_stickArea, eventData.position, eventData.pressEventCamera, out Vector2 local);
            _stick = Vector2.ClampMagnitude(local / _stickRadius, 1f);
            UpdateKnob();
        }

        private void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointer)
            {
                return;
            }

            _pointer = -1;
            _stick = Vector2.zero;
            UpdateKnob();
        }

        private void UpdateKnob()
        {
            if (_knob != null)
            {
                _knob.anchoredPosition = new Vector2(Mathf.Round(_stick.x * _stickRadius), Mathf.Round(_stick.y * _stickRadius));
            }
        }

        private void Update()
        {
            if (!IsShown)
            {
                return;
            }

            Vector2 dir = _stick;
            Keyboard k = Keyboard.current;
            if (_pointer == -1 && k != null)
            {
                Vector2 key = Vector2.zero;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed)
                {
                    key.x -= 1f;
                }

                if (k.dKey.isPressed || k.rightArrowKey.isPressed)
                {
                    key.x += 1f;
                }

                if (k.wKey.isPressed || k.upArrowKey.isPressed)
                {
                    key.y += 1f;
                }

                if (k.sKey.isPressed || k.downArrowKey.isPressed)
                {
                    key.y -= 1f;
                }

                dir = key.normalized;
                if (k.spaceKey.wasPressedThisFrame && _hideButton.interactable)
                {
                    _hideButton.onClick.Invoke();
                }

                if (k.eKey.wasPressedThisFrame && _takeButton.interactable)
                {
                    _takeButton.onClick.Invoke();
                }
            }

            _sendTimer -= Time.deltaTime;
            // Зупинку шлемо одразу, рух — 15 разів на секунду.
            bool stopped = dir == Vector2.zero && _lastSent != Vector2.zero;
            if (_sendTimer <= 0f || stopped)
            {
                _sendTimer = 1f / SendRate;
                if (dir != _lastSent)
                {
                    // У симуляції Y дивиться вниз.
                    Send(HeistAction.Move(new Vec2(dir.x, -dir.y)));
                    _lastSent = dir;
                }
            }
        }
    }
}
