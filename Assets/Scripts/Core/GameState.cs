using System;
using System.Collections.Generic;
using UnityEngine;

namespace Banda.Core
{
    public enum GamePhase
    {
        Menu,
        Lobby,
        Heist,
        Result,
    }

    public enum HeistOutcome
    {
        None,
        Success,
        Caught,
        Timeout,
    }

    /// <summary>Рядок списку банди в лобі — те, що бачить кожен гравець.</summary>
    public sealed class LobbyPlayer
    {
        public string Name = string.Empty;
        public RoleId Role = RoleId.None;
        public bool IsHost;
        public bool IsMe;
        public bool Connected = true;
    }

    /// <summary>Підсумок справи для екрана результату.</summary>
    public sealed class HeistSummary
    {
        public HeistOutcome Outcome = HeistOutcome.None;
        public int TimeUsedSeconds;
        public int MaxAlarm;
        public int SafeMistakes;
    }

    /// <summary>
    /// Глобальний стан гри (аналог autoload GameState з Godot).
    /// Фазу гри змінює тільки GameState; ScreenRouter слухає PhaseChanged і показує потрібний екран.
    ///
    /// НЕ ПЕРЕНЕСЕНО (крок 2 перенесення): мережа. Зараз кімната існує лише локально —
    /// гравець-хост сам у лобі. Мережеві методи позначені явно й повертають зрозуміле повідомлення.
    /// </summary>
    [DefaultExecutionOrder(-100)] // стан готовий раніше за екрани, що на нього підписуються
    public sealed class GameState : MonoBehaviour
    {
        public const int MaxPlayers = 5;
        public const int MinPlayersToStart = 1; // етап 1: можна почати й самому — для тестів
        private const string NamePrefsKey = "banda.player_name";
        private const int MaxNameLength = 14;

        public static GameState Instance { get; private set; }

        public event Action<GamePhase> PhaseChanged;
        public event Action PlayersChanged;
        public event Action<RoleId> RoleChanged;
        public event Action<HeistSummary> ResultReceived;
        public event Action<string> Notice;

        public GamePhase Phase { get; private set; } = GamePhase.Menu;
        public string MyName { get; private set; } = string.Empty;
        public RoleId MyRole { get; private set; } = RoleId.None;
        public string RoomCode { get; private set; } = string.Empty;
        public bool IsHost { get; private set; }
        public HeistSummary LastResult { get; private set; } = new HeistSummary();

        private readonly List<LobbyPlayer> _players = new List<LobbyPlayer>();
        private float _heistStartedAt;

        public IReadOnlyList<LobbyPlayer> LobbyPlayers => _players;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            MyName = PlayerPrefs.GetString(NamePrefsKey, string.Empty);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetMyName(string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length > MaxNameLength)
            {
                trimmed = trimmed.Substring(0, MaxNameLength);
            }

            MyName = trimmed;
            PlayerPrefs.SetString(NamePrefsKey, MyName);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Створює кімнату. ЗАРАЗ ЛОКАЛЬНО: мережевий хост (маяк у Wi-Fi, порт 7777) — крок 2 перенесення.
        /// </summary>
        public void HostRoom()
        {
            IsHost = true;
            RoomCode = UnityEngine.Random.Range(1000, 10000).ToString();
            MyRole = RoleId.None;
            _players.Clear();
            _players.Add(new LobbyPlayer { Name = MyName, IsHost = true, IsMe = true });
            Notice?.Invoke("Мережа ще не перенесена: кімната лише на цьому пристрої");
            SetPhase(GamePhase.Lobby);
            PlayersChanged?.Invoke();
        }

        public bool IsRoleTaken(RoleId role)
        {
            foreach (LobbyPlayer player in _players)
            {
                if (!player.IsMe && player.Role == role)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Запит на роль. На хості перевіряється одразу; у мережевій версії піде RPC до хоста.</summary>
        public void RequestPickRole(RoleId role)
        {
            if (Phase != GamePhase.Lobby)
            {
                return;
            }

            if (role != RoleId.None && (!Roles.IsAvailable(role) || IsRoleTaken(role)))
            {
                Notice?.Invoke("Цю роль узяти не можна");
                return;
            }

            MyRole = role;
            foreach (LobbyPlayer player in _players)
            {
                if (player.IsMe)
                {
                    player.Role = role;
                }
            }

            RoleChanged?.Invoke(role);
            PlayersChanged?.Invoke();
        }

        public bool CanStart()
        {
            if (!IsHost || _players.Count < MinPlayersToStart)
            {
                return false;
            }

            foreach (LobbyPlayer player in _players)
            {
                if (player.Connected && player.Role == RoleId.None)
                {
                    return false;
                }
            }

            return true;
        }

        public void StartHeist()
        {
            if (!CanStart())
            {
                return;
            }

            _heistStartedAt = Time.unscaledTime;
            SetPhase(GamePhase.Heist);
        }

        /// <summary>
        /// Завершення справи. ЗАГЛУШКА ЛОГІКИ: симуляції пограбування (HeistSim) ще немає в Unity —
        /// результат приходить з тестового екрана справи.
        /// </summary>
        public void FinishHeist(HeistOutcome outcome)
        {
            if (Phase != GamePhase.Heist || !IsHost)
            {
                return;
            }

            LastResult = new HeistSummary
            {
                Outcome = outcome,
                TimeUsedSeconds = Mathf.RoundToInt(Time.unscaledTime - _heistStartedAt),
                MaxAlarm = outcome == HeistOutcome.Caught ? 100 : 0,
                SafeMistakes = 0,
            };
            SetPhase(GamePhase.Result);
            ResultReceived?.Invoke(LastResult);
        }

        public void BackToLobby()
        {
            if (!IsHost || Phase != GamePhase.Result)
            {
                return;
            }

            SetPhase(GamePhase.Lobby);
            PlayersChanged?.Invoke();
        }

        public void LeaveRoom()
        {
            IsHost = false;
            RoomCode = string.Empty;
            MyRole = RoleId.None;
            _players.Clear();
            SetPhase(GamePhase.Menu);
        }

        public void ShowNotice(string text)
        {
            Notice?.Invoke(text);
        }

        private void SetPhase(GamePhase newPhase)
        {
            if (Phase == newPhase)
            {
                return;
            }

            Phase = newPhase;
            PhaseChanged?.Invoke(newPhase);
        }
    }
}
