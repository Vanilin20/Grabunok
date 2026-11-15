using System;
using System.Collections.Generic;
using System.IO;
using Banda.Core;
using Banda.Heist;

namespace Banda.Net
{
    /// <summary>
    /// Хост кімнати — чиста логіка без рушія й без сокетів (транспорт підставляється через IServerLink).
    /// Авторитетний: тут живе лобі й HeistSession. Клієнти надсилають лише дії, кожен отримує знімок лише своєї ролі
    /// (HeistSession.SnapshotFor(token)). Гравець — це токен, а не з'єднання: після обриву він повертається на ту саму роль.
    /// Власне місце хоста — «локальний клієнт» (LocalId), що йде повз мережу.
    /// </summary>
    public sealed class RoomHost
    {
        public const int LocalId = -1;
        public const int NoClient = -2;
        public const int MaxPlayers = 5;
        /// <summary>ТЗ: хост оновлює стан 10–20 разів на секунду.</summary>
        public const float SendRate = 15f;
        /// <summary>Скільки лобі тримає місце гравця, що пропав (потім роль звільняється).</summary>
        public const float LobbyGraceSeconds = 15f;
        /// <summary>З'єднання без Hello вважається чужим і скидається.</summary>
        public const float HelloTimeoutSeconds = 8f;

        private sealed class Seat
        {
            public string Token = string.Empty;
            public string Name = string.Empty;
            public RoleId Role = RoleId.None;
            public int ClientId = NoClient;
            public bool Connected;
            public bool IsHost;
            public float OfflineFor;
        }

        private readonly IServerLink _link;
        private readonly Func<HeistLevel> _levelFactory;
        private readonly Func<int> _seedFactory;
        private readonly Func<RoleId, bool> _roleAvailable;
        private readonly int _minPlayers;
        private readonly List<Seat> _seats = new List<Seat>();
        private readonly Dictionary<int, float> _pending = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _closing = new Dictionary<int, float>();
        private const float RejectLinger = 0.6f;
        private readonly Queue<byte[]> _localInbound = new Queue<byte[]>();
        private readonly Queue<byte[]> _localOutbound = new Queue<byte[]>();
        private LocalSeatLink _localLink;
        private HeistSession _session;
        private HeistSummary _summary;
        private float _sendAccumulator;

        public RoomHost(IServerLink link, string roomCode, Func<HeistLevel> levelFactory, Func<int> seedFactory,
            int minPlayers = 1, Func<RoleId, bool> roleAvailable = null)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            Code = roomCode ?? string.Empty;
            _levelFactory = levelFactory ?? throw new ArgumentNullException(nameof(levelFactory));
            _seedFactory = seedFactory ?? (() => 1);
            _minPlayers = Math.Max(1, minPlayers);
            // MVP (ТЗ): Хакер, Тінь, Зломщик. Решту ролей відкриють пізніші етапи.
            _roleAvailable = roleAvailable ?? (r => r == RoleId.Hacker || r == RoleId.Shadow || r == RoleId.Safecracker);
            _link.ClientConnected += OnClientConnected;
            _link.ClientDisconnected += OnClientDisconnected;
            _link.MessageReceived += OnMessage;
        }

        public string Code { get; }

        public RoomPhase Phase { get; private set; } = RoomPhase.Lobby;

        public bool IsClosed { get; private set; }

        /// <summary>Справа, що йде (null поза Heist/Finished). Потрібна тестам і вікну хоста.</summary>
        public HeistSession Session => _session;

        public int SeatCount => _seats.Count;

        /// <summary>Місце самого хоста: його екрани говорять із хостом через нього, повз мережу.</summary>
        public IClientLink CreateLocalLink()
        {
            if (_localLink == null)
            {
                _localLink = new LocalSeatLink(this);
            }

            return _localLink;
        }

        // ------------------------------------------------------------------ життєвий цикл

        /// <summary>Рухати час: черги локального місця, таймаути, тік справи, розсилка знімків 15 разів на секунду.</summary>
        public void Update(float delta)
        {
            if (IsClosed)
            {
                return;
            }

            while (_localInbound.Count > 0)
            {
                OnMessage(LocalId, _localInbound.Dequeue());
            }

            AgePending(delta);
            AgeClosing(delta);
            AgeOfflineSeats(delta);
            if (Phase == RoomPhase.Heist && _session != null)
            {
                _session.Tick(delta);
                if (_summary != null)
                {
                    FinishHeist();
                }
                else
                {
                    _sendAccumulator += delta;
                    if (_sendAccumulator >= 1f / SendRate)
                    {
                        _sendAccumulator = 0f;
                        SendSnapshots();
                    }
                }
            }

            FlushLocal();
        }

        /// <summary>Закрити кімнату: усі клієнти отримують зрозумілу причину.</summary>
        public void Close(string reason)
        {
            if (IsClosed)
            {
                return;
            }

            byte[] data = WireCodec.Encode(MsgType.Closed, w => WireCodec.WriteString(w, reason));
            foreach (Seat seat in _seats)
            {
                if (seat.Connected)
                {
                    Send(seat.ClientId, data);
                }
            }

            FlushLocal();
            IsClosed = true;
        }

        // ------------------------------------------------------------------ вхідні

        private void OnClientConnected(int clientId)
        {
            _pending[clientId] = 0f;
        }

        private void OnClientDisconnected(int clientId)
        {
            _pending.Remove(clientId);
            Seat seat = FindByClient(clientId);
            if (seat == null)
            {
                return;
            }

            seat.Connected = false;
            seat.ClientId = NoClient;
            seat.OfflineFor = 0f;
            if (_session != null)
            {
                // Тінь, що лишилась без «пальця», зупиняється — інакше вона бігла б далі сама.
                if (seat.Role == RoleId.Shadow)
                {
                    _session.Submit(seat.Token, HeistAction.Move(Vec2.Zero));
                }

                _session.MarkDisconnected(seat.Token);
            }

            BroadcastLobby();
        }

        private void OnMessage(int clientId, byte[] data)
        {
            try
            {
                MsgType type = WireCodec.ReadType(data, out BinaryReader r);
                using (r)
                {
                    if (type == MsgType.Hello)
                    {
                        OnHello(clientId, r);
                        return;
                    }

                    Seat seat = FindByClient(clientId);
                    if (seat == null)
                    {
                        return; // без Hello нічого не приймаємо
                    }

                    switch (type)
                    {
                        case MsgType.PickRole:
                            OnPickRole(seat, r);
                            break;
                        case MsgType.StartHeist:
                            OnStart(seat);
                            break;
                        case MsgType.Action:
                            OnAction(seat, r);
                            break;
                        case MsgType.RequestState:
                            SendState(seat);
                            break;
                        case MsgType.BackToLobby:
                            OnBackToLobby(seat);
                            break;
                    }
                }
            }
            catch (Exception e) when (e is ProtocolException || e is EndOfStreamException || e is ArgumentException || e is IOException)
            {
                // Сміття від клієнта не повинно валити хост: ігноруємо пакет.
            }
        }

        private void OnHello(int clientId, BinaryReader r)
        {
            byte version = r.ReadByte();
            string token = WireCodec.ReadString(r);
            string name = SanitizeName(WireCodec.ReadString(r));
            _pending.Remove(clientId);
            if (version != WireCodec.Version)
            {
                Reject(clientId, "Версія гри на цьому телефоні інша. Онови гру до тієї ж версії, що в хоста.");
                return;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                Reject(clientId, "Не вдалося впізнати пристрій. Перезапусти гру.");
                return;
            }

            Seat seat = FindByToken(token);
            if (seat != null)
            {
                // Повернення після обриву: те саме місце, та сама роль.
                if (seat.Connected && seat.ClientId != clientId && seat.ClientId != NoClient && seat.ClientId != LocalId)
                {
                    _link.Disconnect(seat.ClientId); // старе зависле з'єднання
                }

                seat.ClientId = clientId;
                seat.Connected = true;
                seat.OfflineFor = 0f;
                if (name.Length > 0)
                {
                    seat.Name = name;
                }

                _session?.Reconnect(token);
                SendWelcome(seat, true);
                BroadcastLobby();
                SendState(seat);
                return;
            }

            if (Phase == RoomPhase.Heist)
            {
                Reject(clientId, "Справа вже почалась. Приєднатися можуть лише ті, хто в банді.");
                return;
            }

            if (Phase == RoomPhase.Finished)
            {
                Reject(clientId, "Справу завершено. Хост має повернути банду в лобі.");
                return;
            }

            if (_seats.Count >= MaxPlayers)
            {
                Reject(clientId, "Кімната повна.");
                return;
            }

            seat = new Seat
            {
                Token = token,
                Name = name.Length > 0 ? name : "Гравець",
                ClientId = clientId,
                Connected = true,
                IsHost = clientId == LocalId,
            };
            _seats.Add(seat);
            SendWelcome(seat, false);
            BroadcastLobby();
        }

        private void OnPickRole(Seat seat, BinaryReader r)
        {
            int raw = r.ReadInt32();
            if (Phase != RoomPhase.Lobby)
            {
                Notice(seat, "Ролі можна міняти лише в лобі.");
                return;
            }

            if (!Enum.IsDefined(typeof(RoleId), raw))
            {
                return;
            }

            RoleId role = (RoleId)raw;
            if (role != RoleId.None)
            {
                if (!_roleAvailable(role))
                {
                    Notice(seat, "Ця роль ще недоступна.");
                    return;
                }

                foreach (Seat other in _seats)
                {
                    if (other != seat && other.Role == role)
                    {
                        Notice(seat, "Цю роль уже взяв інший гравець.");
                        BroadcastLobby();
                        return;
                    }
                }
            }

            seat.Role = role;
            BroadcastLobby();
        }

        private void OnStart(Seat seat)
        {
            if (!seat.IsHost)
            {
                Notice(seat, "Почати справу може тільки хост.");
                return;
            }

            if (Phase != RoomPhase.Lobby)
            {
                return;
            }

            if (!CanStart())
            {
                Notice(seat, "Почати можна, коли кожен обрав роль.");
                return;
            }

            // Хто відпав у лобі — не в справі.
            _seats.RemoveAll(s => !s.Connected);
            _summary = null;
            _sendAccumulator = 0f;
            _session = new HeistSession(_levelFactory(), _seedFactory());
            _session.Finished += s => _summary = s;
            foreach (Seat s in _seats)
            {
                _session.AddPlayer(s.Token, s.Name, s.Role);
            }

            Phase = RoomPhase.Heist;
            BroadcastLobby();
            foreach (Seat s in _seats)
            {
                SendState(s);
            }
        }

        private void OnAction(Seat seat, BinaryReader r)
        {
            HeistAction action = WireCodec.ReadAction(r);
            if (Phase != RoomPhase.Heist || _session == null)
            {
                return;
            }

            // Роль бере хост із реєстру, а не з дії — чужу дію підробити не можна.
            _session.Submit(seat.Token, action);
        }

        private void OnBackToLobby(Seat seat)
        {
            if (!seat.IsHost || Phase != RoomPhase.Finished)
            {
                return;
            }

            _session = null;
            _summary = null;
            _seats.RemoveAll(s => !s.Connected);
            Phase = RoomPhase.Lobby;
            BroadcastLobby();
        }

        // ------------------------------------------------------------------ справа

        private void FinishHeist()
        {
            HeistSummary summary = _summary;
            Phase = RoomPhase.Finished;
            byte[] finished = WireCodec.Encode(MsgType.Finished, w => WireCodec.WriteSummary(w, summary));
            foreach (Seat seat in _seats)
            {
                if (!seat.Connected)
                {
                    continue;
                }

                SendSnapshot(seat); // останній знімок із фінальним станом
                Send(seat.ClientId, finished);
            }

            BroadcastLobby();
        }

        private void SendSnapshots()
        {
            foreach (Seat seat in _seats)
            {
                if (seat.Connected)
                {
                    SendSnapshot(seat);
                }
            }
        }

        private void SendSnapshot(Seat seat)
        {
            RoleSnapshot snap = _session.SnapshotFor(seat.Token);
            Send(seat.ClientId, WireCodec.Encode(MsgType.Snapshot, w => WireCodec.WriteSnapshot(w, snap)));
        }

        /// <summary>Повний стан для одного гравця: статика ролі + знімок (+ підсумок, якщо справу вже завершено).</summary>
        private void SendState(Seat seat)
        {
            if (_session == null || (Phase != RoomPhase.Heist && Phase != RoomPhase.Finished))
            {
                return;
            }

            RoleStatic stat = _session.StaticFor(seat.Token);
            if (stat != null)
            {
                Send(seat.ClientId, WireCodec.Encode(MsgType.Static, w => WireCodec.WriteStatic(w, stat)));
            }

            SendSnapshot(seat);
            if (Phase == RoomPhase.Finished && _summary != null)
            {
                HeistSummary summary = _summary;
                Send(seat.ClientId, WireCodec.Encode(MsgType.Finished, w => WireCodec.WriteSummary(w, summary)));
            }
        }

        // ------------------------------------------------------------------ лобі

        private bool CanStart()
        {
            int connected = 0;
            foreach (Seat seat in _seats)
            {
                if (!seat.Connected)
                {
                    continue;
                }

                connected++;
                if (seat.Role == RoleId.None)
                {
                    return false;
                }
            }

            return connected >= _minPlayers;
        }

        private void BroadcastLobby()
        {
            bool canStart = Phase == RoomPhase.Lobby && CanStart();
            foreach (Seat seat in _seats)
            {
                if (!seat.Connected)
                {
                    continue;
                }

                LobbyState lobby = new LobbyState
                {
                    RoomCode = Code,
                    Phase = Phase,
                    YourIndex = _seats.IndexOf(seat),
                    CanStart = seat.IsHost && canStart,
                };
                foreach (Seat s in _seats)
                {
                    lobby.Players.Add(new LobbyPlayerInfo { Name = s.Name, Role = s.Role, IsHost = s.IsHost, Connected = s.Connected });
                }

                Send(seat.ClientId, WireCodec.Encode(MsgType.Lobby, w => WireCodec.WriteLobby(w, lobby)));
            }
        }

        private void AgeClosing(float delta)
        {
            if (_closing.Count == 0)
            {
                return;
            }

            List<int> keys = new List<int>(_closing.Keys);
            foreach (int id in keys)
            {
                _closing[id] += delta;
                if (_closing[id] >= RejectLinger)
                {
                    _closing.Remove(id);
                    _link.Disconnect(id);
                }
            }
        }

        private void AgePending(float delta)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            List<int> expired = null;
            List<int> keys = new List<int>(_pending.Keys);
            foreach (int id in keys)
            {
                _pending[id] += delta;
                if (_pending[id] > HelloTimeoutSeconds)
                {
                    (expired ?? (expired = new List<int>())).Add(id);
                }
            }

            if (expired != null)
            {
                foreach (int id in expired)
                {
                    _pending.Remove(id);
                    _link.Disconnect(id);
                }
            }
        }

        private void AgeOfflineSeats(float delta)
        {
            if (Phase != RoomPhase.Lobby)
            {
                return;
            }

            bool removed = false;
            for (int i = _seats.Count - 1; i >= 0; i--)
            {
                Seat seat = _seats[i];
                if (seat.Connected || seat.IsHost)
                {
                    continue;
                }

                seat.OfflineFor += delta;
                if (seat.OfflineFor > LobbyGraceSeconds)
                {
                    _seats.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                BroadcastLobby();
            }
        }

        // ------------------------------------------------------------------ відправка

        private void SendWelcome(Seat seat, bool resumed)
        {
            WelcomeInfo info = new WelcomeInfo { Ok = true, RoomCode = Code, IsHost = seat.IsHost, Role = seat.Role, Resumed = resumed };
            Send(seat.ClientId, WireCodec.Encode(MsgType.Welcome, w => WireCodec.WriteWelcome(w, info)));
        }

        private void Reject(int clientId, string error)
        {
            WelcomeInfo info = new WelcomeInfo { Ok = false, Error = error, RoomCode = Code };
            Send(clientId, WireCodec.Encode(MsgType.Welcome, w => WireCodec.WriteWelcome(w, info)));
            if (clientId != LocalId)
            {
                // Розрив із затримкою: відмова мусить встигнути дійти (NGO може скинути чергу відправки одразу після Disconnect).
                _closing[clientId] = 0f;
            }
        }

        private void Notice(Seat seat, string text)
        {
            Send(seat.ClientId, WireCodec.Encode(MsgType.Notice, w => WireCodec.WriteString(w, text)));
        }

        private void Send(int clientId, byte[] data)
        {
            if (clientId == LocalId)
            {
                _localOutbound.Enqueue(data);
            }
            else if (clientId != NoClient)
            {
                _link.Send(clientId, data);
            }
        }

        private void FlushLocal()
        {
            if (_localLink == null)
            {
                _localOutbound.Clear();
                return;
            }

            while (_localOutbound.Count > 0)
            {
                _localLink.Deliver(_localOutbound.Dequeue());
            }
        }

        internal void EnqueueFromLocal(byte[] data) => _localInbound.Enqueue(data);

        // ------------------------------------------------------------------ пошук

        private Seat FindByClient(int clientId)
        {
            if (clientId == NoClient)
            {
                return null;
            }

            foreach (Seat seat in _seats)
            {
                if (seat.Connected && seat.ClientId == clientId)
                {
                    return seat;
                }
            }

            return null;
        }

        private Seat FindByToken(string token)
        {
            foreach (Seat seat in _seats)
            {
                if (seat.Token == token)
                {
                    return seat;
                }
            }

            return null;
        }

        private static string SanitizeName(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();
            return trimmed.Length > 14 ? trimmed.Substring(0, 14) : trimmed;
        }
    }

    /// <summary>Місце хоста: клієнтська сторона без мережі. Повідомлення ходять через черги RoomHost (доставка в Update).</summary>
    public sealed class LocalSeatLink : IClientLink
    {
        private readonly RoomHost _host;

        internal LocalSeatLink(RoomHost host)
        {
            _host = host;
        }

        public bool IsConnected => !_host.IsClosed;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> MessageReceived;

        public void Send(byte[] data) => _host.EnqueueFromLocal(data);

        public void Disconnect()
        {
            Disconnected?.Invoke();
        }

        internal void Deliver(byte[] data) => MessageReceived?.Invoke(data);
    }
}
