using System.Collections.Generic;
using Banda.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>
    /// Лобі: код кімнати, список банди, вибір ролі, старт (тільки хост).
    /// Ролі з пізніших етапів показані заблокованими з номером етапу.
    /// </summary>
    public sealed class LobbyScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _codeLabel;
        [SerializeField] private TMP_Text _ipLabel;
        [SerializeField] private RectTransform _playersBox;
        [SerializeField] private GameObject _playerRowTemplate;
        [SerializeField] private RectTransform _rolesBox;
        [SerializeField] private Button _roleButtonTemplate;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private TMP_Text _hintLabel;
        [Tooltip("Суцільна заливка обраної ролі (фарбується кольором ролі).")]
        [SerializeField] private Sprite _selectedSprite;
        [Tooltip("Портрети ролей у порядку Roles.All: звичайний, обраний (рамка світиться), заблокований (силует із замком).")]
        [SerializeField] private Sprite[] _portraitNormal = new Sprite[0];
        [SerializeField] private Sprite[] _portraitSelected = new Sprite[0];
        [SerializeField] private Sprite[] _portraitLocked = new Sprite[0];

        private readonly Dictionary<RoleId, Button> _roleButtons = new Dictionary<RoleId, Button>();
        private readonly List<GameObject> _playerRows = new List<GameObject>();

        private void Awake()
        {
            _playerRowTemplate.SetActive(false);
            _roleButtonTemplate.gameObject.SetActive(false);
            BuildRoleButtons();
        }

        private void OnEnable()
        {
            GameState state = GameState.Instance;
            if (state == null)
            {
                return;
            }

            state.PlayersChanged += Refresh;
            state.RoleChanged += OnRoleChanged;
            _startButton.onClick.AddListener(OnStartPressed);
            _leaveButton.onClick.AddListener(OnLeavePressed);
            // НЕ ПЕРЕНЕСЕНО (крок 2): локальні IP для ручного підключення — частина мережевого модуля.
            _ipLabel.text = state.IsHost ? "IP: мережа — крок 2" : string.Empty;
            _startButton.gameObject.SetActive(state.IsHost);
            Refresh();
        }

        private void OnDisable()
        {
            GameState state = GameState.Instance;
            if (state != null)
            {
                state.PlayersChanged -= Refresh;
                state.RoleChanged -= OnRoleChanged;
            }

            _startButton.onClick.RemoveListener(OnStartPressed);
            _leaveButton.onClick.RemoveListener(OnLeavePressed);
        }

        private void BuildRoleButtons()
        {
            foreach (RoleId role in Roles.All)
            {
                Button button = Instantiate(_roleButtonTemplate, _rolesBox);
                button.gameObject.name = $"Role_{role}";
                button.gameObject.SetActive(true);
                RoleId captured = role;
                button.onClick.AddListener(() => OnRolePressed(captured));
                _roleButtons[role] = button;
            }
        }

        /// <summary>Портрет ролі в кнопці (дочірній Image «Portrait» шаблону кнопки).</summary>
        private static void SetPortrait(Button button, RoleId role, Sprite[] set)
        {
            Transform child = button.transform.Find("Portrait");
            int index = System.Array.IndexOf(Roles.All, role);
            if (child == null || index < 0 || index >= set.Length)
            {
                return;
            }

            Image image = child.GetComponent<Image>();
            image.sprite = set[index];
            image.enabled = set[index] != null;
        }

        private void OnRoleChanged(RoleId _)
        {
            Refresh();
        }

        private void OnRolePressed(RoleId role)
        {
            GameState state = GameState.Instance;
            if (state == null)
            {
                return;
            }

            // Повторне натискання своєї ролі — відмовитися від неї.
            state.RequestPickRole(state.MyRole == role ? RoleId.None : role);
        }

        private void OnStartPressed() => GameState.Instance?.StartHeist();

        private void OnLeavePressed() => GameState.Instance?.LeaveRoom();

        private void Refresh()
        {
            GameState state = GameState.Instance;
            if (state == null)
            {
                return;
            }

            _codeLabel.text = string.IsNullOrEmpty(state.RoomCode) ? "····" : state.RoomCode;
            RefreshPlayers(state);
            foreach (KeyValuePair<RoleId, Button> pair in _roleButtons)
            {
                RoleId role = pair.Key;
                Button button = pair.Value;
                bool mine = state.MyRole == role;
                bool taken = state.IsRoleTaken(role);
                bool available = Roles.IsAvailable(role);
                button.interactable = available && !taken;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                string roleName = Roles.DisplayName(role).ToUpperInvariant();
                if (!available)
                {
                    label.text = $"{roleName}  · етап {Roles.UnlockStage(role)}";
                }
                else if (taken)
                {
                    label.text = $"{roleName}  · зайнято";
                }
                else
                {
                    label.text = $"{(mine ? "> " : string.Empty)}{roleName}  · {Roles.Hint(role)}";
                }

                // Обрана роль — заливка кольором ролі, текст темний; решта — текст кольором ролі.
                Color roleColor = Roles.ColorOf(role);
                Image background = button.targetGraphic as Image;
                if (background != null)
                {
                    background.overrideSprite = mine ? _selectedSprite : null;
                    background.color = mine ? roleColor : Color.white;
                }

                SetPortrait(button, role, !available || taken ? _portraitLocked : mine ? _portraitSelected : _portraitNormal);
                UiButtonFeedback feedback = button.GetComponent<UiButtonFeedback>();
                if (feedback != null)
                {
                    Color text = mine ? Palette.Night0 : roleColor;
                    feedback.SetColors(text, mine ? Palette.Night0 : Color.Lerp(roleColor, Color.white, 0.4f), Palette.Steel2);
                }
            }

            if (state.IsHost)
            {
                _startButton.interactable = state.CanStart();
                _hintLabel.text = _startButton.interactable ? "Банда готова" : "Почати можна, коли кожен обрав роль";
            }
            else
            {
                _hintLabel.text = "Хост почне справу, коли всі оберуть ролі";
            }
        }

        private void RefreshPlayers(GameState state)
        {
            foreach (GameObject row in _playerRows)
            {
                Destroy(row);
            }

            _playerRows.Clear();
            foreach (LobbyPlayer player in state.LobbyPlayers)
            {
                GameObject row = Instantiate(_playerRowTemplate, _playersBox);
                row.SetActive(true);
                _playerRows.Add(row);
                Image dot = row.transform.Find("Dot").GetComponent<Image>();
                TMP_Text nameLabel = row.transform.Find("Name").GetComponent<TMP_Text>();
                TMP_Text roleLabel = row.transform.Find("Role").GetComponent<TMP_Text>();
                dot.color = player.Connected ? Roles.ColorOf(player.Role) : Palette.Slate;
                string suffix = (player.IsHost ? " ★" : string.Empty) + (player.IsMe ? " (ти)" : string.Empty);
                nameLabel.text = player.Name + suffix;
                if (!player.Connected)
                {
                    roleLabel.text = "немає зв'язку";
                    roleLabel.color = Palette.Danger;
                }
                else
                {
                    roleLabel.text = player.Role != RoleId.None ? Roles.DisplayName(player.Role) : "обирає…";
                    roleLabel.color = Roles.ColorOf(player.Role);
                }
            }
        }
    }
}
