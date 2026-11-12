using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>
    /// Відгук кнопки: звук натискання / відмови та колір підпису за станом
    /// (робоча — бурштинова «неонова трубка», наведена — світліша, вимкнена — сіра без світіння).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Color _normalColor = new Color(0.969f, 0.788f, 0.353f);
        [SerializeField] private Color _hoverColor = new Color(1f, 0.949f, 0.769f);
        [SerializeField] private Color _disabledColor = new Color(0.545f, 0.576f, 0.659f);

        private Button _button;
        private bool _hovered;
        private bool _lastInteractable;
        private bool _lastHovered;
        private bool _colorsOverridden;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_label == null)
            {
                _label = GetComponentInChildren<TMP_Text>(true);
            }

            _button.onClick.AddListener(OnClick);
            ApplyColor(true);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnDisable()
        {
            _hovered = false;
        }

        private void LateUpdate()
        {
            ApplyColor(false);
        }

        /// <summary>Перефарбувати підпис (кнопки ролей мають власний колір).</summary>
        public void SetColors(Color normal, Color hover, Color disabled)
        {
            _normalColor = normal;
            _hoverColor = hover;
            _disabledColor = disabled;
            _colorsOverridden = true;
            ApplyColor(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Вимкнена кнопка не викликає onClick — даємо глухий звук, щоб гравець зрозумів відмову.
            if (!_button.IsInteractable() && UiSound.Instance != null)
            {
                UiSound.Instance.PlayDeny();
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        private void OnClick()
        {
            if (UiSound.Instance != null)
            {
                UiSound.Instance.PlayClick();
            }
        }

        private void ApplyColor(bool force)
        {
            if (_label == null || _button == null)
            {
                return;
            }

            bool interactable = _button.IsInteractable();
            if (!force && interactable == _lastInteractable && _hovered == _lastHovered && !_colorsOverridden)
            {
                return;
            }

            _colorsOverridden = false;
            _lastInteractable = interactable;
            _lastHovered = _hovered;
            _label.color = !interactable ? _disabledColor : (_hovered ? _hoverColor : _normalColor);
        }
    }
}
