using System;
using Banda.Heist;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Banda.Net
{
    /// <summary>
    /// Хост кімнати на цьому телефоні: NGO-сервер (без власного клієнта) + RoomHost + місце самого хоста повз мережу.
    /// Транспорт тут один — UnityTransport на заданій адресі. Як гравці знайдуть цю адресу (Wi-Fi-маяк, Relay), вирішує IRoomService.
    /// </summary>
    public sealed class HostSession : IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly NgoServerLink _link;
        private readonly System.Collections.Generic.List<IDisposable> _attached = new System.Collections.Generic.List<IDisposable>();
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

        /// <summary>Прикріпити те, що живе стільки ж, скільки кімната (маяк Wi-Fi, сесія Relay): знищується разом із нею.</summary>
        public void Attach(IDisposable resource) => _attached.Add(resource);

        /// <summary>
        /// Запустити хост. listen — адреса, на якій слухати (0.0.0.0 — усі інтерфейси). Для Relay сюди замість цього
        /// передається вже налаштований транспорт (див. StartWithTransport).
        /// </summary>
        public static HostSession Start(ushort port, string roomCode, string hostName, string token, out string error)
        {
            NetworkManager nm = NetRuntime.CreateManager(out UnityTransport transport);
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            return Finish(nm, roomCode, hostName, token, out error);
        }

        /// <summary>Хост на вже створеному й налаштованому транспорті (Relay: SetRelayServerData зроблено викликачем).</summary>
        public static HostSession StartWithManager(NetworkManager nm, string roomCode, string hostName, string token, out string error)
        {
            return Finish(nm, roomCode, hostName, token, out error);
        }

        private static HostSession Finish(NetworkManager nm, string roomCode, string hostName, string token, out string error)
        {
            error = null;
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

            NetDriver.After(0.5f, () =>
            {
                _link.Dispose();
                NetRuntime.DestroyManager();
            });
        }
    }

    /// <summary>Клієнт кімнати: NGO-клієнт + RoomClient. Reconnect повертає той самий токен на ту саму роль.</summary>
    public sealed class ClientSession : IDisposable
    {
        private readonly Func<NetworkManager> _managerFactory;
        private NgoClientLink _link;
        private bool _disposed;

        private ClientSession(Func<NetworkManager> managerFactory, string token, string name)
        {
            _managerFactory = managerFactory;
            Client = new RoomClient(StartLink(out string error), token, name);
            StartError = error;
        }

        public RoomClient Client { get; }

        /// <summary>Не null, якщо клієнт не зміг навіть почати з'єднання (до Hello).</summary>
        public string StartError { get; private set; }

        public static ClientSession Connect(string ip, ushort port, string token, string name)
        {
            return ConnectWith(() =>
            {
                NetworkManager nm = NetRuntime.CreateManager(out UnityTransport transport);
                transport.SetConnectionData(ip, port);
                return nm;
            }, token, name);
        }

        /// <summary>Клієнт на довільному транспорті: фабрика щоразу створює й налаштовує NetworkManager (Relay: нове JoinAllocation).</summary>
        public static ClientSession ConnectWith(Func<NetworkManager> managerFactory, string token, string name)
        {
            return new ClientSession(managerFactory, token, name);
        }

        private IClientLink StartLink(out string error)
        {
            error = null;
            _link?.Dispose();
            NetworkManager nm = _managerFactory();
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

        /// <summary>Підключитися знову (після обриву). Новий NetworkManager створюється наступного кадру: старий має встигнути згаснути.</summary>
        public void Reconnect()
        {
            if (_disposed)
            {
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
            NetRuntime.DestroyManager();
        }
    }
}
