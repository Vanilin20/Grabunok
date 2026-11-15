using System;
using Unity.Collections;
using Unity.Netcode;

namespace Banda.Net
{
    /// <summary>
    /// Кадрування повідомлень Банди поверх іменованих повідомлень NGO: «довжина + байти».
    /// Іменовані повідомлення — адресні (одному клієнтові), без NetworkObject і сцен: стан справи НЕ йде через NetworkVariable,
    /// тож «кожна роль бачить тільки своє» гарантує сама симуляція (HeistSession.SnapshotFor), а не видимість об'єктів.
    /// </summary>
    internal static class NgoFraming
    {
        public const string MessageName = "banda";
        public const int MaxMessageBytes = 64 * 1024;
        // Знімки йдуть із частотою 15 Гц, статика більша за MTU: усе — надійно, послідовно, зі фрагментацією.
        public const NetworkDelivery Delivery = NetworkDelivery.ReliableFragmentedSequenced;

        public static void Send(NetworkManager nm, ulong clientId, byte[] data)
        {
            if (nm == null || nm.CustomMessagingManager == null || data == null || data.Length > MaxMessageBytes)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(data.Length + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(data.Length);
                writer.WriteBytesSafe(data, data.Length);
                nm.CustomMessagingManager.SendNamedMessage(MessageName, clientId, writer, Delivery);
            }
        }

        public static byte[] Read(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int length);
            if (length < 0 || length > MaxMessageBytes)
            {
                return null;
            }

            byte[] data = new byte[length];
            reader.ReadBytesSafe(ref data, length);
            return data;
        }
    }

    /// <summary>Сторона хоста поверх NGO. Власне місце хоста сюди не потрапляє (ідентифікатор 0 пропускається): воно йде повз мережу.</summary>
    public sealed class NgoServerLink : IServerLink, IDisposable
    {
        private readonly NetworkManager _nm;
        private bool _disposed;

        /// <summary>Створювати ПІСЛЯ StartHost/StartServer: тоді вже існує CustomMessagingManager.</summary>
        public NgoServerLink(NetworkManager nm)
        {
            _nm = nm ?? throw new ArgumentNullException(nameof(nm));
            _nm.OnClientConnectedCallback += OnConnected;
            _nm.OnClientDisconnectCallback += OnDisconnected;
            _nm.CustomMessagingManager.RegisterNamedMessageHandler(NgoFraming.MessageName, OnNamedMessage);
        }

        public event Action<int> ClientConnected;
        public event Action<int> ClientDisconnected;
        public event Action<int, byte[]> MessageReceived;

        public void Send(int clientId, byte[] data) => NgoFraming.Send(_nm, (ulong)clientId, data);

        public void Disconnect(int clientId)
        {
            if (_nm != null && _nm.IsListening)
            {
                _nm.DisconnectClient((ulong)clientId);
            }
        }

        private void OnConnected(ulong id)
        {
            if (id != NetworkManager.ServerClientId)
            {
                ClientConnected?.Invoke((int)id);
            }
        }

        private void OnDisconnected(ulong id)
        {
            if (id != NetworkManager.ServerClientId)
            {
                ClientDisconnected?.Invoke((int)id);
            }
        }

        private void OnNamedMessage(ulong sender, FastBufferReader reader)
        {
            byte[] data = NgoFraming.Read(reader);
            if (data != null && sender != NetworkManager.ServerClientId)
            {
                MessageReceived?.Invoke((int)sender, data);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _nm.OnClientConnectedCallback -= OnConnected;
            _nm.OnClientDisconnectCallback -= OnDisconnected;
            if (_nm.CustomMessagingManager != null)
            {
                _nm.CustomMessagingManager.UnregisterNamedMessageHandler(NgoFraming.MessageName);
            }
        }
    }

    /// <summary>Сторона клієнта поверх NGO. Створювати після StartClient; Connected спрацює, коли хост прийняв з'єднання.</summary>
    public sealed class NgoClientLink : IClientLink, IDisposable
    {
        private readonly NetworkManager _nm;
        private bool _connected;
        private bool _disposed;

        public NgoClientLink(NetworkManager nm)
        {
            _nm = nm ?? throw new ArgumentNullException(nameof(nm));
            _nm.OnClientConnectedCallback += OnConnected;
            _nm.OnClientDisconnectCallback += OnDisconnected;
            _nm.CustomMessagingManager?.RegisterNamedMessageHandler(NgoFraming.MessageName, OnNamedMessage);
        }

        /// <summary>Підключення не вдалося ще до початку (StartClient повернув false): повідомити екран як звичайний розрив.</summary>
        public void RaiseFailed() => Disconnected?.Invoke();

        public bool IsConnected => _connected && _nm != null && _nm.IsConnectedClient;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<byte[]> MessageReceived;

        public void Send(byte[] data)
        {
            if (IsConnected)
            {
                NgoFraming.Send(_nm, NetworkManager.ServerClientId, data);
            }
        }

        public void Disconnect()
        {
            if (_nm != null && (_nm.IsListening || _nm.IsConnectedClient))
            {
                _nm.Shutdown();
            }
        }

        private void OnConnected(ulong id)
        {
            if (id == _nm.LocalClientId && !_connected)
            {
                _connected = true;
                Connected?.Invoke();
            }
        }

        private void OnDisconnected(ulong id)
        {
            bool wasConnected = _connected;
            _connected = false;
            // Невдала спроба підключення теж приходить сюди: екран покаже «не вдалося підключитись».
            Disconnected?.Invoke();
            if (!wasConnected)
            {
                return;
            }
        }

        private void OnNamedMessage(ulong sender, FastBufferReader reader)
        {
            byte[] data = NgoFraming.Read(reader);
            if (data != null)
            {
                MessageReceived?.Invoke(data);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _nm.OnClientConnectedCallback -= OnConnected;
            _nm.OnClientDisconnectCallback -= OnDisconnected;
            if (_nm.CustomMessagingManager != null)
            {
                _nm.CustomMessagingManager.UnregisterNamedMessageHandler(NgoFraming.MessageName);
            }
        }
    }
}
