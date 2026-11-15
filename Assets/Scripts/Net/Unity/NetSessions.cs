using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Banda.Heist;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Banda.Net
{
    /// <summary>
    /// Хост кімнати на цьому телефоні: NGO + RoomHost + місце самого хоста повз мережу.
    /// Звідки береться з'єднання (прямий UnityTransport для Wi-Fi чи Relay-сесія сервісів) — справа сервісу кімнат, не хоста.
    /// </summary>
    public sealed class HostSession : IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly NgoServerLink _link;
        private readonly List<IDisposable> _attached = new List<IDisposable>();
        private readonly List<IDisposable> _attachedLate = new List<IDisposable>();
        private bool _sessionManaged;
        private bool _disposed;

        private HostSession(NetworkManager manager, NgoServerLink link, RoomHost room, RoomClient local)
        {
            _manager = manager;
            _link = link;
            Room = room;
            LocalClient = local;
            NetDriver.Instance.Tick += OnTick;
        }

        public RoomHost Room { get; }

        /// <summary>Місце самого хоста в кімнаті: його екрани беруть ITransport звідси (LocalClient.Transport).</summary>
        public RoomClient LocalClient { get; }

        public NetworkManager Manager => _manager;

        /// <summary>Прикріпити те, що живе стільки ж, скільки кімната (маяк Wi-Fi): знищується одразу, як хост закриває кімнату.</summary>
        public void Attach(IDisposable resource) => _attached.Add(resource);

        /// <summary>Те саме, але знищується вже ПІСЛЯ того, як клієнти отримали причину закриття (сесія Relay).</summary>
        public void AttachLate(IDisposable resource) => _attachedLate.Add(resource);

        /// <summary>NetworkManager запущено сесією сервісів: гасити її через LeaveAsync (AttachLate), а не Shutdown.</summary>
        public void MarkSessionManaged() => _sessionManaged = true;

        /// <summary>Wi-Fi: прямий транспорт. Слухає на всіх інтерфейсах, порт — port.</summary>
        public static HostSession Start(ushort port, string roomCode, string hostName, string token, out string error)
        {
            NetworkManager nm = NetRuntime.CreateManager(out UnityTransport transport);
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            bool started;
            try
            {
                started = nm.StartServer();
            }
            catch (Exception e)
            {
                started = false;
                UnityEngine.Debug.LogWarning("StartServer: " + e.Message);
            }

            if (!started)
            {
                NetRuntime.DestroyManager();
                error = "Не вдалося відкрити кімнату: порт зайнятий або мережа недоступна.";
                return null;
            }

            error = null;
            return Create(nm, roomCode, hostName, token);
        }

        /// <summary>Хост на NetworkManager, який уже запущено (сесія Relay викликає StartHost сама).</summary>
        public static HostSession StartOnRunningManager(NetworkManager nm, string roomCode, string hostName, string token)
        {
            HostSession session = Create(nm, roomCode, hostName, token);
            session.MarkSessionManaged();
            return session;
        }

        private static HostSession Create(NetworkManager nm, string roomCode, string hostName, string token)
        {
            NgoServerLink link = new NgoServerLink(nm);
            RoomHost room = new RoomHost(link, roomCode, HeistLevel.CornerStore, () => Environment.TickCount & 0x7fffffff);
            RoomClient local = new RoomClient(room.CreateLocalLink(), token, hostName);
            return new HostSession(nm, link, room, local);
        }

        private void OnTick(float dt)
        {
            if (!_disposed)
            {
                Room.Update(dt);
            }
        }

        /// <summary>Закрити кімнату: гравці отримують причину, транспорт гасне трохи згодом, щоб повідомлення дійшли.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Room.Close("Хост вийшов. Справу завершено.");
            Room.Update(0f);
            _disposed = true;
            NetDriver.Instance.Tick -= OnTick;
            foreach (IDisposable d in _attached)
            {
                d.Dispose();
            }

            bool managed = _sessionManaged;
            NetDriver.After(0.5f, () =>
            {
                _link.Dispose();
                foreach (IDisposable d in _attachedLate)
                {
                    d.Dispose();
                }

                NetDriver.After(managed ? 1.0f : 0f, () => NetRuntime.DestroyManager(_manager, managed));
            });
        }
    }

    /// <summary>Клієнт кімнати: NGO-клієнт + RoomClient. Reconnect повертає той самий токен на ту саму роль.</summary>
    public sealed class ClientSession : IDisposable
    {
        private readonly Func<NetworkManager> _managerFactory;
        private readonly Func<Task<NetworkManager>> _reconnectAsync;
        private readonly List<IDisposable> _attached = new List<IDisposable>();
        private NgoClientLink _link;
        private NetworkManager _nm;
        private bool _sessionManaged;
        private bool _disposed;

        /// <summary>Прямий транспорт (Wi-Fi): NetworkManager створюється й запускається тут.</summary>
        private ClientSession(Func<NetworkManager> managerFactory, string token, string name)
        {
            _managerFactory = managerFactory;
            Client = new RoomClient(StartLink(out string error), token, name);
            StartError = error;
        }

        /// <summary>Сесія сервісів: NetworkManager уже запущено й підключено; для повернення після обриву — reconnectAsync.</summary>
        private ClientSession(NetworkManager running, Func<Task<NetworkManager>> reconnectAsync, string token, string name)
        {
            _reconnectAsync = reconnectAsync;
            _sessionManaged = true;
            _nm = running;
            _link = new NgoClientLink(running);
            Client = new RoomClient(_link, token, name);
        }

        public RoomClient Client { get; }

        /// <summary>Не null, якщо клієнт не зміг навіть почати з'єднання (до Hello).</summary>
        public string StartError { get; private set; }

        public void Attach(IDisposable resource) => _attached.Add(resource);

        public static ClientSession Connect(string ip, ushort port, string token, string name)
        {
            return new ClientSession(() =>
            {
                NetworkManager nm = NetRuntime.CreateManager(out UnityTransport transport);
                transport.SetConnectionData(ip, port);
                return nm;
            }, token, name);
        }

        public static ClientSession OnRunningManager(NetworkManager running, Func<Task<NetworkManager>> reconnectAsync, string token, string name)
        {
            return new ClientSession(running, reconnectAsync, token, name);
        }

        private IClientLink StartLink(out string error)
        {
            error = null;
            _link?.Dispose();
            NetworkManager nm = _managerFactory();
            _nm = nm;
            bool ok;
            try
            {
                ok = nm != null && nm.StartClient();
            }
            catch (Exception e)
            {
                ok = false;
                UnityEngine.Debug.LogWarning("StartClient: " + e.Message);
            }

            if (!ok)
            {
                error = "Не вдалося почати підключення. Перевір мережу.";
            }

            NgoClientLink link = new NgoClientLink(nm);
            _link = link;
            if (!ok)
            {
                NetDriver.After(0.1f, link.RaiseFailed);
            }

            return link;
        }

        /// <summary>Підключитися знову (після обриву). Той самий токен: хост поверне роль і стан.</summary>
        public async void Reconnect()
        {
            if (_disposed)
            {
                return;
            }

            if (_reconnectAsync != null)
            {
                try
                {
                    _link?.Dispose();
                    NetworkManager nm = await _reconnectAsync();
                    if (_disposed || nm == null)
                    {
                        return;
                    }

                    _nm = nm;
                    _link = new NgoClientLink(nm);
                    Client.Rebind(_link);
                }
                catch (Exception e)
                {
                    StartError = RelayErrors.Describe(e);
                    UnityEngine.Debug.LogWarning("Reconnect: " + e.Message);
                    // Лишаємо стан Lost: екран покаже «зв'язок пропав» і запропонує спробувати ще.
                }

                return;
            }

            NetRuntime.DestroyManager();
            NetDriver.After(0.2f, () =>
            {
                if (_disposed)
                {
                    return;
                }

                IClientLink link = StartLink(out string error);
                StartError = error;
                Client.Rebind(link);
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _link?.Dispose();
            foreach (IDisposable d in _attached)
            {
                d.Dispose();
            }

            if (_sessionManaged)
            {
                // Сесію гасить LeaveAsync (прикріплений вище); обʼєкт мережі знищуємо після нього.
                NetworkManager old = _nm;
                NetDriver.After(1.0f, () => NetRuntime.DestroyManager(old, true));
            }
            else
            {
                NetRuntime.DestroyManager(_nm, false);
            }
        }
    }
}
