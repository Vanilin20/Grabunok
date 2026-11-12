using Banda.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>
    /// Вхід у кімнату: код із 4 цифр (кімнату знаходимо пошуком у локальній мережі)
    /// або запасний шлях — IP хоста вручну.
    ///
    /// НЕ ПЕРЕНЕСЕНО (крок 2): пошук кімнат і підключення. Екран показує це прямо в статусі,
    /// а не вдає, що шукає.
    /// </summary>
    public sealed class JoinRoomScreen : MonoBehaviour
    {
        private const string NetworkMissing = "Мережа ще не перенесена в Unity (крок 2)";

        [SerializeField] private TMP_InputField _codeEdit;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _roomsHint;
        [SerializeField] private TMP_InputField _ipEdit;
        [SerializeField] private Button _ipButton;
        [SerializeField] private Button _backButton;

        private void OnEnable()
        {
            _codeEdit.SetTextWithoutNotify(string.Empty);
            _codeEdit.onValueChanged.AddListener(OnCodeChanged);
            _ipButton.onClick.AddListener(OnIpPressed);
            _backButton.onClick.AddListener(OnBackPressed);
            _roomsHint.text = "пошук кімнат вимкнено:\nмережа — наступний крок";
            _roomsHint.color = Palette.Muted;
            SetStatus("Введи код з екрана хоста", Palette.Muted);
        }

        private void OnDisable()
        {
            _codeEdit.onValueChanged.RemoveListener(OnCodeChanged);
            _ipButton.onClick.RemoveListener(OnIpPressed);
            _backButton.onClick.RemoveListener(OnBackPressed);
        }

        private void OnCodeChanged(string text)
        {
            string digits = string.Empty;
            foreach (char ch in text)
            {
                if (ch >= '0' && ch <= '9')
                {
                    digits += ch;
                }
            }

            if (digits != text)
            {
                _codeEdit.SetTextWithoutNotify(digits);
                _codeEdit.caretPosition = digits.Length;
            }

            if (digits.Length < 4)
            {
                SetStatus(digits.Length == 0 ? "Введи код з екрана хоста" : string.Empty, Palette.Muted);
                return;
            }

            SetStatus($"Кімнату {digits} не знайти: {NetworkMissing}", Palette.Lamp);
        }

        private void OnIpPressed()
        {
            string ip = _ipEdit.text.Trim();
            SetStatus(string.IsNullOrEmpty(ip) ? "Введи IP хоста" : NetworkMissing, Palette.Danger);
        }

        private void OnBackPressed()
        {
            ScreenRouter.Instance?.ShowMenu();
        }

        private void SetStatus(string text, Color color)
        {
            _statusLabel.text = text;
            _statusLabel.color = color;
        }
    }
}
