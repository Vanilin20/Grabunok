using System.Text;
using Banda.Core;
using Banda.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>
    /// Вхід у кімнату: код із 4 цифр (спершу пошук у Wi-Fi, потім онлайн) або запасний шлях — IP хоста вручну.
    /// Поки екран відкритий, у підказці живим списком видно кімнати, що оголошені в цій Wi-Fi.
    /// </summary>
    public sealed class JoinRoomScreen : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _codeEdit;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _roomsHint;
        [SerializeField] private TMP_InputField _ipEdit;
        [SerializeField] private Button _ipButton;
        [SerializeField] private Button _backButton;

        private LanBeaconListener _listener;
        private float _roomsTimer;

        private void Update()
        {
            _roomsTimer += Time.unscaledDeltaTime;
            if (_roomsTimer > 0.5f)
            {
                _roomsTimer = 0f;
                RefreshRooms();
            }
        }

        private void RefreshRooms()
        {
            if (_listener == null)
            {
                _roomsHint.text = "кімнати поруч не видно:\nвведи код або IP";
                _roomsHint.color = Palette.Muted;
                return;
            }

            System.Collections.Generic.List<LanRoom> rooms = _listener.Rooms.Snapshot();
            if (rooms.Count == 0)
            {
                _roomsHint.text = "поруч у Wi-Fi\nкімнат не видно";
                _roomsHint.color = Palette.Muted;
                return;
            }

            StringBuilder sb = new StringBuilder("поруч:\n");
            foreach (LanRoom room in rooms)
            {
                sb.Append(room.Info.Code).Append(' ').Append(room.Info.HostName).Append(' ').Append(room.Info.Players).Append('/').Append(RoomHost.MaxPlayers).Append('\n');
            }

            _roomsHint.text = sb.ToString().TrimEnd();
            _roomsHint.color = Palette.Lamp;
        }

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
            _listener?.Dispose();
            _listener = null;
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

            SetStatus(string.Empty, Palette.Muted);
            GameState.Instance?.JoinByCode(digits); // діалог мережі покаже пошук, невдачу чи пустить у лобі
        }

        private void OnIpPressed()
        {
            string ip = _ipEdit.text.Trim();
            if (string.IsNullOrEmpty(ip))
            {
                SetStatus("Введи IP хоста", Palette.Danger);
                return;
            }

            GameState.Instance?.JoinByIp(ip);
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
