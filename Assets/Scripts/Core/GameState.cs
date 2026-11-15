using System;
using System.Collections;
using System.Collections.Generic;
using Banda.Heist;
using Banda.Net;
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

    /// <summary>Що зараз із зʼєднанням: екрани показують це людською мовою (пошук, підключення, обрив, хост вийшов).</summary>
    public enum NetStatus
    {
        /// <summary>Мережі немає: меню або локальна кімната («гаряче місце»).</summary>
        Offline,
        Searching,
        Connecting,
        Online,
        /// <summary>Дія не вдалась (нема кімнати, нема інтернету…): пояснення в NetMessage, кнопка «Добре».</summary>
        Failed,
        /// <summary>Зв'язок пропав, гра пробує повернутись.</summary>
        Lost,
        /// <summary>Хост закрив кімнату або повернутись не вдалося: далі лише в меню.</summary>
        Ended,
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

    /// <summary>
    /// Глобальний стан гри (аналог autoload GameState з Godot).
    /// Фазу гри змінює тільки GameState; ScreenRouter слухає PhaseChanged і показує потрібний екран.
    ///
    /// Кімната буває локальною («гаряче місце» на одному пристрої) або мережевою (Wi-Fi чи Relay). У мережевій
    /// кімнаті стан лобі й фаза приходять від хоста через RoomClient; екрани цього не помічають.
    /// </summary>
    [DefaultExecutionOrder(-100)] // стан готовий раніше за екрани, що на нього підписуються
    public sealed class GameState : MonoBehaviour
    {
        public const int MaxPlayers = 5;
        public const int MinPlayersToStart = 1; // етап 1: можна почати й самому — для тестів
        private const string NamePrefsKey = "banda.player_name";
        private const int MaxNameLength = 14;

        public static GameState Instance { get; private set; }

        /// <summary>Автотести (UiDrive): ім'я й токен гравця живуть лише в пам'яті й не затирають збережені налаштування.</summary>
        public static bool Ephemeral { get; set; }

        private static string _ephemeralToken;

        /// <summary>Токен для тестового запуску (у кожного процесу свій).</summary>
        public static void UseEphemeralToken(string token)
        {
            Ephemeral = true;
            _ephemeralToken = token;
        }

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

        private const string TokenPrefsKey = "banda.player_token";
        private const int ReconnectAttempts = 8;
        private const float ReconnectInterval = 4f;

        private readonly List<LobbyPlayer> _players = new List<LobbyPlayer>();
        private float _heistStartedAt;
        private HostSession _hostSession;
        private ClientSession _clientSession;
        private RoomClient _room;
        private Coroutine _reconnectRoutine;
        private bool _busy;
        private int _attempt; // номер спроби: скасована спроба, що вже летіла, не має впливати на стан

        public IReadOnlyList<LobbyPlayer> LobbyPlayers => _players;

        public NetStatus Net { get; private set; } = NetStatus.Offline;

        /// <summary>Пояснення поточного стану мережі для екрана (що відбувається чи що робити).</summary>
        public string NetMessage { get; private set; } = string.Empty;

        public event Action NetChanged;

        /// <summary>Мережева кімната (Wi-Fi чи Relay), а не локальне «гаряче місце».</summary>
        public bool IsNetworked => _room != null;

        public RoomKind? NetKind { get; private set; }

        /// <summary>Транспорт справи для екранів ролей у мережевій кімнаті.</summary>
        public ITransport NetTransport => _room != null ? _room.Transport : null;

        /// <summary>Зайнятий асинхронною дією (створення кімнати, пошук): кнопки не натискаються двічі.</summary>
        public bool Busy => _busy;

        /// <summary>IP цього телефона в Wi-Fi: хост показує його для запасного підключення.</summary>
        public string LocalAddress { get; private set; } = string.Empty;

        private static string PlayerToken()
        {
            if (Ephemeral)
            {
                return _ephemeralToken;
            }

            string token = PlayerPrefs.GetString(TokenPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(token))
            {
                token = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(TokenPrefsKey, token);
                PlayerPrefs.Save();
            }

            return token;
        }

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

            DetachRoom();
            _hostSession?.Dispose();
            _clientSession?.Dispose();
        }

        public void SetMyName(string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length > MaxNameLength)
            {
                trimmed = trimmed.Substring(0, MaxNameLength);
            }

            MyName = trimmed;
            if (Ephemeral)
            {
                return;
            }

            PlayerPrefs.SetString(NamePrefsKey, MyName);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Локальна кімната («гаряче місце»): усі ролі на цьому пристрої, мережі немає. Для перевірки на одному ПК.
        /// </summary>
        public void HostRoom()
        {
            IsHost = true;
            RoomCode = UnityEngine.Random.Range(1000, 10000).ToString();
            MyRole = RoleId.None;
            _players.Clear();
            _players.Add(new LobbyPlayer { Name = MyName, IsHost = true, IsMe = true });
            NetKind = null;
            SetPhase(GamePhase.Lobby);
            PlayersChanged?.Invoke();
        }

        // ------------------------------------------------------------------ мережа

        private void SetNet(NetStatus status, string message)
        {
            Net = status;
            NetMessage = message ?? string.Empty;
            NetChanged?.Invoke();
        }

        /// <summary>Скасувати пошук чи створення кімнати, що ще триває.</summary>
        public void CancelBusy()
        {
            if (!_busy)
            {
                return;
            }

            _attempt++;
            _busy = false;
            SetNet(NetStatus.Offline, string.Empty);
        }

        /// <summary>Закрити повідомлення про невдачу (кнопка «Добре»).</summary>
        public void DismissNetMessage()
        {
            if (Net == NetStatus.Failed)
            {
                SetNet(NetStatus.Offline, string.Empty);
            }
        }

        /// <summary>Створити мережеву кімнату: поруч (Wi-Fi) чи онлайн (Relay).</summary>
        public async void HostOnlineRoom(RoomKind kind, string forcedCode = null)
        {
            if (_busy || _room != null)
            {
                return;
            }

            _busy = true;
            int attempt = ++_attempt;
            SetNet(NetStatus.Connecting, kind == RoomKind.Lan ? "Відкриваю кімнату в Wi-Fi…" : "Створюю онлайн-кімнату…");
            IRoomService service = kind == RoomKind.Lan ? (IRoomService)new LanRoomService() : new RelayRoomService();
            ServiceResult<HostSession> result;
            try
            {
                result = await service.CreateRoomAsync(MyName, PlayerToken(), forcedCode);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Створення кімнати: " + e);
                result = ServiceResult<HostSession>.Fail(RelayErrors.Describe(e));
            }

            if (attempt != _attempt)
            {
                result.Value?.Dispose(); // гравець устиг скасувати
                return;
            }

            _busy = false;
            if (!result.Ok)
            {
                SetNet(NetStatus.Failed, result.Error);
                return;
            }

            _hostSession = result.Value;
            NetKind = kind;
            IsHost = true;
            RoomCode = _hostSession.Room.Code;
            LocalAddress = kind == RoomKind.Lan ? LanAddress.Find() : string.Empty;
            MyRole = RoleId.None;
            AttachRoom(_hostSession.LocalClient);
            SetNet(NetStatus.Online, string.Empty);
            SetPhase(GamePhase.Lobby);
            RefreshFromLobby();
        }

        /// <summary>Приєднатися за кодом з 4 цифр: спершу Wi-Fi, потім онлайн.</summary>
        public async void JoinByCode(string code)
        {
            if (_busy || _room != null)
            {
                return;
            }

            _busy = true;
            int attempt = ++_attempt;
            string token = PlayerToken();
            SetNet(NetStatus.Searching, "Шукаю кімнату " + code + " у Wi-Fi…");
            ServiceResult<ClientSession> found;
            RoomKind kind = RoomKind.Lan;
            try
            {
                found = await new LanRoomService().JoinRoomAsync(code, MyName, token);
                if (!found.Ok && found.NotFound)
                {
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        found = ServiceResult<ClientSession>.Fail("Кімнату " + code + " не знайдено в Wi-Fi, а онлайн недоступно: немає інтернету.", true);
                    }
                    else
                    {
                        SetNet(NetStatus.Searching, "У Wi-Fi немає. Шукаю кімнату " + code + " онлайн…");
                        kind = RoomKind.Relay;
                        found = await new RelayRoomService().JoinRoomAsync(code, MyName, token);
                        if (!found.Ok && found.NotFound)
                        {
                            found = ServiceResult<ClientSession>.Fail("Кімнату " + code + " не знайдено ні в Wi-Fi, ні онлайн. Перевір код: можливо, хост уже вийшов.", true);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Приєднання: " + e);
                found = ServiceResult<ClientSession>.Fail(RelayErrors.Describe(e));
            }

            if (attempt != _attempt)
            {
                found.Value?.Dispose();
                return;
            }

            _busy = false;
            EnterAsClient(found, kind, code);
        }

        /// <summary>Запасний шлях: підключитись до хоста за IP (Wi-Fi без broadcast, точка доступу).</summary>
        public async void JoinByIp(string ip)
        {
            if (_busy || _room != null)
            {
                return;
            }

            _busy = true;
            int attempt = ++_attempt;
            SetNet(NetStatus.Connecting, "Підключаюсь до " + ip + "…");
            ServiceResult<ClientSession> found;
            try
            {
                found = await new LanRoomService().JoinByIpAsync(ip, MyName, PlayerToken());
            }
            catch (Exception e)
            {
                found = ServiceResult<ClientSession>.Fail(RelayErrors.Describe(e));
            }

            if (attempt != _attempt)
            {
                found.Value?.Dispose();
                return;
            }

            _busy = false;
            EnterAsClient(found, RoomKind.Lan, string.Empty);
        }

        private void EnterAsClient(ServiceResult<ClientSession> result, RoomKind kind, string code)
        {
            if (!result.Ok)
            {
                SetNet(NetStatus.Failed, result.Error);
                return;
            }

            _clientSession = result.Value;
            NetKind = kind;
            IsHost = false;
            RoomCode = code;
            MyRole = RoleId.None;
            AttachRoom(_clientSession.Client);
            SetNet(NetStatus.Online, string.Empty);
            SetPhase(GamePhase.Lobby);
            RefreshFromLobby();
        }

        private void AttachRoom(RoomClient room)
        {
            _room = room;
            _room.StateChanged += OnRoomState;
            _room.LobbyChanged += RefreshFromLobby;
            _room.Notice += OnRoomNotice;
            _room.Transport.Finished += OnNetFinished;
        }

        private void DetachRoom()
        {
            if (_room == null)
            {
                return;
            }

            _room.StateChanged -= OnRoomState;
            _room.LobbyChanged -= RefreshFromLobby;
            _room.Notice -= OnRoomNotice;
            _room.Transport.Finished -= OnNetFinished;
            _room = null;
        }

        private void OnRoomNotice(string text) => Notice?.Invoke(text);

        private void RefreshFromLobby()
        {
            if (_room == null)
            {
                return;
            }

            LobbyState lobby = _room.Lobby;
            _players.Clear();
            if (lobby != null)
            {
                for (int i = 0; i < lobby.Players.Count; i++)
                {
                    LobbyPlayerInfo info = lobby.Players[i];
                    _players.Add(new LobbyPlayer { Name = info.Name, Role = info.Role, IsHost = info.IsHost, IsMe = i == lobby.YourIndex, Connected = info.Connected });
                }

                if (string.IsNullOrEmpty(RoomCode) && !string.IsNullOrEmpty(lobby.RoomCode))
                {
                    RoomCode = lobby.RoomCode;
                }

                IsHost = _room.IsHost;
            }
            else
            {
                _players.Add(new LobbyPlayer { Name = MyName, IsHost = IsHost, IsMe = true });
            }

            RoleId role = _room.Role;
            if (role != MyRole)
            {
                MyRole = role;
                RoleChanged?.Invoke(role);
            }

            if (lobby != null)
            {
                if (lobby.Phase == RoomPhase.Lobby && Phase != GamePhase.Lobby)
                {
                    SetPhase(GamePhase.Lobby);
                }
                else if (lobby.Phase == RoomPhase.Heist && Phase != GamePhase.Heist)
                {
                    _heistStartedAt = Time.unscaledTime;
                    SetPhase(GamePhase.Heist);
                }
            }

            PlayersChanged?.Invoke();
        }

        private void OnNetFinished(HeistSummary summary)
        {
            if (summary == null)
            {
                return;
            }

            LastResult = summary;
            SetPhase(GamePhase.Result);
            ResultReceived?.Invoke(LastResult);
        }

        private void OnRoomState(ClientState state)
        {
            switch (state)
            {
                case ClientState.Joined:
                    if (Net == NetStatus.Lost)
                    {
                        StopReconnect();
                        SetNet(NetStatus.Online, string.Empty);
                        Notice?.Invoke("Повернувся в кімнату");
                    }

                    RefreshFromLobby();
                    break;
                case ClientState.Lost:
                    if (Net == NetStatus.Online && _reconnectRoutine == null)
                    {
                        _reconnectRoutine = StartCoroutine(ReconnectLoop());
                    }

                    break;
                case ClientState.Closed:
                    EndRoom(_room != null && !string.IsNullOrEmpty(_room.StateMessage) ? _room.StateMessage : "Хост закрив кімнату.");
                    break;
                case ClientState.Rejected:
                    EndRoom(_room != null ? _room.StateMessage : "Хост не пустив у кімнату.");
                    break;
            }
        }

        private IEnumerator ReconnectLoop()
        {
            SetNet(NetStatus.Lost, "Зв'язок пропав. Повертаюсь у кімнату…");
            yield return new WaitForSecondsRealtime(1.5f);
            for (int attempt = 1; attempt <= ReconnectAttempts; attempt++)
            {
                SetNet(NetStatus.Lost, $"Зв'язок пропав. Повертаюсь у кімнату… спроба {attempt} з {ReconnectAttempts}");
                _clientSession?.Reconnect();
                yield return new WaitForSecondsRealtime(ReconnectInterval);
                if (Net != NetStatus.Lost)
                {
                    yield break;
                }
            }

            _reconnectRoutine = null;
            EndRoom("Повернутись не вдалося. Хост, схоже, вийшов, або зник Wi-Fi чи інтернет.");
        }

        private void StopReconnect()
        {
            if (_reconnectRoutine != null)
            {
                StopCoroutine(_reconnectRoutine);
                _reconnectRoutine = null;
            }
        }

        /// <summary>Кімната закінчилась не з нашої волі (хост вийшов, не вдалося повернутись): усім зрозумілий екран.</summary>
        private void EndRoom(string reason)
        {
            StopReconnect();
            DetachRoom();
            _hostSession?.Dispose();
            _clientSession?.Dispose();
            _hostSession = null;
            _clientSession = null;
            SetNet(NetStatus.Ended, reason);
        }

        private void TearDownNet()
        {
            StopReconnect();
            DetachRoom();
            _hostSession?.Dispose();
            _clientSession?.Dispose();
            _hostSession = null;
            _clientSession = null;
            NetKind = null;
            LocalAddress = string.Empty;
            SetNet(NetStatus.Offline, string.Empty);
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

            if (_room != null)
            {
                _room.PickRole(role); // роль закріпить хост; лобі оновиться його відповіддю
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
            if (_room != null)
            {
                return IsHost && _room.Lobby != null && _room.Lobby.CanStart;
            }

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

            if (_room != null)
            {
                _room.RequestStart(); // фаза зміниться, коли хост розішле лобі
                return;
            }

            _heistStartedAt = Time.unscaledTime;
            SetPhase(GamePhase.Heist);
        }

        /// <summary>
        /// Завершення справи: підсумок приходить від HeistSession (через ITransport) на хості.
        /// </summary>
        public void FinishHeist(HeistSummary summary)
        {
            if (Phase != GamePhase.Heist || !IsHost || summary == null)
            {
                return;
            }

            LastResult = summary;
            SetPhase(GamePhase.Result);
            ResultReceived?.Invoke(LastResult);
        }

        /// <summary>Здатися посеред справи (кнопка паузи) — як провал за тривогою, але чесно позначено.</summary>
        public void AbortHeist()
        {
            if (Phase != GamePhase.Heist)
            {
                return;
            }

            if (_room != null)
            {
                LeaveRoom(); // у мережевій справі «здатися» = вийти з кімнати
                return;
            }

            FinishHeist(new HeistSummary
            {
                Outcome = HeistOutcome.Caught,
                TimeUsedSeconds = Mathf.RoundToInt(Time.unscaledTime - _heistStartedAt),
            });
        }

        public void BackToLobby()
        {
            if (!IsHost || Phase != GamePhase.Result)
            {
                return;
            }

            if (_room != null)
            {
                _room.RequestBackToLobby();
                return;
            }

            SetPhase(GamePhase.Lobby);
            PlayersChanged?.Invoke();
        }

        public void LeaveRoom()
        {
            TearDownNet();
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
