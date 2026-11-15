using System;
using System.Collections.Generic;

namespace Banda.Net
{
    /// <summary>Сторона хоста: з'єднання з клієнтами. Реалізації — NGO (Wi-Fi, Relay), пам'ять (тести), локальне місце хоста.</summary>
    public interface IServerLink
    {
        event Action<int> ClientConnected;
        event Action<int> ClientDisconnected;
        event Action<int, byte[]> MessageReceived;

        void Send(int clientId, byte[] data);

        /// <summary>Розірвати з'єднання з клієнтом (хост відхилив його чи кімната закривається).</summary>
        void Disconnect(int clientId);
    }

    /// <summary>Сторона клієнта: з'єднання з хостом.</summary>
    public interface IClientLink
    {
        event Action Connected;
        event Action Disconnected;
        event Action<byte[]> MessageReceived;

        bool IsConnected { get; }

        void Send(byte[] data);

        void Disconnect();
    }

    /// <summary>
    /// Мережа в пам'яті для тестів: хост і клієнти в одному процесі, повідомлення йдуть через чергу й доставляються в Pump().
    /// Дозволяє «обривати» клієнта (Cut) і рахувати, що саме прийшло, без жодного сокета.
    /// </summary>
    public sealed class MemoryNet
    {
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly Dictionary<int, MemoryClientLink> _clients = new Dictionary<int, MemoryClientLink>();
        private int _nextId = 1;

        public MemoryNet()
        {
            Server = new MemoryServerLink(this);
        }

        public MemoryServerLink Server { get; }

        /// <summary>Усі повідомлення, які пішли з хоста до клієнтів (для перевірок «кому що віддали»).</summary>
        public List<KeyValuePair<int, byte[]>> SentToClients { get; } = new List<KeyValuePair<int, byte[]>>();

        public MemoryClientLink Connect()
        {
            int id = _nextId++;
            MemoryClientLink link = new MemoryClientLink(this, id);
            _clients[id] = link;
            _queue.Enqueue(() =>
            {
                link.SetConnected(true);
                Server.RaiseConnected(id);
            });
            return link;
        }

        /// <summary>Обрив з боку мережі (зник Wi-Fi): обидві сторони дізнаються про розрив.</summary>
        public void Cut(MemoryClientLink link)
        {
            Drop(link.Id, true);
        }

        /// <summary>Доставити все, що накопичилось, включно з тим, що породили обробники.</summary>
        public int Pump(int limit = 10000)
        {
            int n = 0;
            while (_queue.Count > 0 && n < limit)
            {
                _queue.Dequeue().Invoke();
                n++;
            }

            return n;
        }

        internal void ToServer(int id, byte[] data)
        {
            byte[] copy = (byte[])data.Clone();
            _queue.Enqueue(() =>
            {
                if (_clients.ContainsKey(id))
                {
                    Server.RaiseMessage(id, copy);
                }
            });
        }

        internal void ToClient(int id, byte[] data)
        {
            byte[] copy = (byte[])data.Clone();
            SentToClients.Add(new KeyValuePair<int, byte[]>(id, copy));
            _queue.Enqueue(() =>
            {
                if (_clients.TryGetValue(id, out MemoryClientLink link) && link.IsConnected)
                {
                    link.RaiseMessage(copy);
                }
            });
        }

        internal void Drop(int id, bool notifyServer)
        {
            if (!_clients.TryGetValue(id, out MemoryClientLink link))
            {
                return;
            }

            if (notifyServer)
            {
                // Обрив мережі: усе, що летіло, втрачено, обидві сторони дізнаються одразу.
                _clients.Remove(id);
                bool was = link.IsConnected;
                link.SetConnected(false);
                _queue.Enqueue(() =>
                {
                    if (was)
                    {
                        link.RaiseDisconnected();
                    }

                    Server.RaiseDisconnected(id);
                });
                return;
            }

            // Розрив з боку хоста: спершу доходить усе, що він встиг надіслати (як у TCP), потім клієнт бачить розрив.
            _queue.Enqueue(() =>
            {
                if (!_clients.Remove(id))
                {
                    return;
                }

                bool was = link.IsConnected;
                link.SetConnected(false);
                if (was)
                {
                    link.RaiseDisconnected();
                }
            });
        }
    }

    public sealed class MemoryServerLink : IServerLink
    {
        private readonly MemoryNet _net;

        internal MemoryServerLink(MemoryNet net)
        {
            _net = net;
        }

        public event Action<int> ClientConnected;
        public event Action<int> ClientDisconnected;
        public event Action<int, byte[]> MessageReceived;

        public void Send(int clientId, byte[] data) => _net.ToClient(clientId, data);

        public void Disconnect(int clientId) => _net.Drop(clientId, false);

        internal void RaiseConnected(int id) => ClientConnected?.Invoke(id);

        internal void RaiseDisconnected(int id) => ClientDisconnected?.Invoke(id);

        internal void RaiseMessage(int id, byte[] data) => MessageReceived?.Invoke(id, data);
    }

    public sealed class MemoryClientLink : IClientLink
    {
        private readonly MemoryNet _net;

        internal MemoryClientLink(MemoryNet net, int id)
        {
            _net = net;
            Id = id;
        }

        public int Id { get; }

        public bool IsConnected { get; private set; }

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> MessageReceived;

        public void Send(byte[] data)
        {
            if (IsConnected)
            {
                _net.ToServer(Id, data);
            }
        }

        public void Disconnect() => _net.Drop(Id, true);

        internal void SetConnected(bool value)
        {
            IsConnected = value;
            if (value)
            {
                Connected?.Invoke();
            }
        }

        internal void RaiseDisconnected() => Disconnected?.Invoke();

        internal void RaiseMessage(byte[] data) => MessageReceived?.Invoke(data);
    }
}
