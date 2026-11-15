using System;
using System.IO;
using Banda.Core;
using Banda.Heist;

namespace Banda.Net
{
    public enum ClientState
    {
        /// <summary>З'єднуюсь із хостом (або чекаю відповіді на Hello).</summary>
        Connecting,

        /// <summary>У кімнаті: лобі чи справа.</summary>
        Joined,

        /// <summary>Хост не прийняв (повна кімната, інша версія, справа йде).</summary>
        Rejected,

        /// <summary>Зв'язок пропав без попередження (Wi-Fi, мобільний інтернет, хост завис).</summary>
        Lost,

        /// <summary>Хост закрив кімнату або вийшов — з причиною.</summary>
        Closed,
    }

    /// <summary>
    /// Клієнт кімнати — чиста логіка без рушія. Говорить із хостом через IClientLink (NGO, пам'ять чи локальне місце хоста).
    /// Віддає екранам ITransport (NetworkTransport): ті самі події, що й у LocalTransport, тож екрани ролей не змінюються.
    /// Гравець — це токен: після обриву Rebind(нове з'єднання) повертає його на ту саму роль зі збереженим станом.
    /// </summary>
    public sealed class RoomClient
    {
        private readonly string _token;
        private string _name;
        private IClientLink _link;
        private bool _helloSent;

        public RoomClient(IClientLink link, string token, string name)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Порожній токен гравця", nameof(token));
            }

            _token = token;
            _name = name ?? string.Empty;
            Transport = new NetworkTransport(this);
            Attach(link);
        }

        public ClientState State { get; private set; } = ClientState.Connecting;

        /// <summary>Людське пояснення поточного стану (відмова, причина закриття) — для екрана.</summary>
        public string StateMessage { get; private set; } = string.Empty;

        public WelcomeInfo Welcome { get; private set; }

        public LobbyState Lobby { get; private set; }

        public NetworkTransport Transport { get; }

        public string Token => _token;

        public RoleId Role
        {
            get
            {
                if (Lobby != null && Lobby.YourIndex >= 0 && Lobby.YourIndex < Lobby.Players.Count)
                {
                    return Lobby.Players[Lobby.YourIndex].Role;
                }

                return Welcome != null ? Welcome.Role : RoleId.None;
            }
        }

        public bool IsHost => Welcome != null && Welcome.IsHost;

        public event Action<ClientState> StateChanged;
        public event Action LobbyChanged;
        public event Action<string> Notice;

        /// <summary>Підключитися знову (нове з'єднання, той самий токен). Хост поверне роль і стан, якщо знає токен.</summary>
        public void Rebind(IClientLink link)
        {
            Attach(link);
        }

        public void SetName(string name) => _name = name ?? string.Empty;

        public void PickRole(RoleId role)
        {
            Send(MsgType.PickRole, w => w.Write((int)role));
        }

        public void RequestStart() => Send(MsgType.StartHeist);

        public void RequestBackToLobby() => Send(MsgType.BackToLobby);

        internal void SendAction(HeistAction action) => Send(MsgType.Action, w => WireCodec.WriteAction(w, action));

        internal void RequestState() => Send(MsgType.RequestState);

        private void Send(MsgType type, Action<BinaryWriter> body = null)
        {
            if (_link != null && _link.IsConnected && _helloSent)
            {
                _link.Send(WireCodec.Encode(type, body));
            }
        }

        // ------------------------------------------------------------------ з'єднання

        private void Attach(IClientLink link)
        {
            if (_link != null)
            {
                _link.Connected -= OnConnected;
                _link.Disconnected -= OnDisconnected;
                _link.MessageReceived -= OnMessage;
            }

            _link = link ?? throw new ArgumentNullException(nameof(link));
            _helloSent = false;
            SetState(ClientState.Connecting, string.Empty);
            _link.Connected += OnConnected;
            _link.Disconnected += OnDisconnected;
            _link.MessageReceived += OnMessage;
            if (_link.IsConnected)
            {
                SendHello();
            }
        }

        private void OnConnected()
        {
            SendHello();
        }

        private void SendHello()
        {
            if (_helloSent)
            {
                return;
            }

            _helloSent = true;
            _link.Send(WireCodec.Encode(MsgType.Hello, w =>
            {
                w.Write(WireCodec.Version);
                WireCodec.WriteString(w, _token);
                WireCodec.WriteString(w, _name);
            }));
        }

        private void OnDisconnected()
        {
            _helloSent = false;
            // Відмова чи закриття вже пояснені хостом — розрив після них не міняє причину.
            if (State == ClientState.Rejected || State == ClientState.Closed)
            {
                return;
            }

            SetState(ClientState.Lost, "Зв'язок із хостом пропав.");
        }

        private void SetState(ClientState state, string message)
        {
            State = state;
            StateMessage = message;
            StateChanged?.Invoke(state);
        }

        // ------------------------------------------------------------------ вхідні

        private void OnMessage(byte[] data)
        {
            try
            {
                MsgType type = WireCodec.ReadType(data, out BinaryReader r);
                using (r)
                {
                    switch (type)
                    {
                        case MsgType.Welcome:
                            OnWelcome(WireCodec.ReadWelcome(r));
                            break;
                        case MsgType.Lobby:
                            Lobby = WireCodec.ReadLobby(r);
                            LobbyChanged?.Invoke();
                            break;
                        case MsgType.Static:
                            Transport.Raise(WireCodec.ReadStatic(r));
                            break;
                        case MsgType.Snapshot:
                            Transport.Raise(WireCodec.ReadSnapshot(r));
                            break;
                        case MsgType.Finished:
                            Transport.Raise(WireCodec.ReadSummary(r));
                            break;
                        case MsgType.Closed:
                            SetState(ClientState.Closed, WireCodec.ReadString(r));
                            break;
                        case MsgType.Notice:
                            Notice?.Invoke(WireCodec.ReadString(r));
                            break;
                    }
                }
            }
            catch (Exception e) when (e is ProtocolException || e is EndOfStreamException || e is ArgumentException || e is IOException)
            {
                // Пошкоджений пакет від хоста: пропускаємо, наступний знімок прийде за 1/15 с.
            }
        }

        private void OnWelcome(WelcomeInfo info)
        {
            Welcome = info;
            if (info.Ok)
            {
                SetState(ClientState.Joined, string.Empty);
            }
            else
            {
                SetState(ClientState.Rejected, info.Error);
            }
        }
    }

    /// <summary>
    /// ITransport для мережевої гри: екрани ролей отримують ті самі події, що й від LocalTransport.
    /// Роль гравця визначає хост; тут вона береться з лобі.
    /// </summary>
    public sealed class NetworkTransport : ITransport
    {
        private readonly RoomClient _client;

        internal NetworkTransport(RoomClient client)
        {
            _client = client;
        }

        public RoleId LocalRole => _client.Role;

        public event Action<RoleStatic> StaticReceived;
        public event Action<RoleSnapshot> SnapshotReceived;
        public event Action<HeistSummary> Finished;
        // У мережі роль не міняється посеред справи (це лише «гаряче місце» на одному пристрої).
        public event Action<RoleId> LocalRoleChanged { add { } remove { } }

        public void Send(HeistAction action)
        {
            if (action != null)
            {
                _client.SendAction(action);
            }
        }

        public void RequestState() => _client.RequestState();

        internal void Raise(RoleStatic stat) => StaticReceived?.Invoke(stat);

        internal void Raise(RoleSnapshot snap) => SnapshotReceived?.Invoke(snap);

        internal void Raise(HeistSummary summary) => Finished?.Invoke(summary);
    }
}
