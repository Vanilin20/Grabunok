using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Banda.Net
{
    /// <summary>
    /// Wi-Fi: UDP-маяк. Хост раз на секунду шле «BANDA1|код|імʼя|гравців|відкрита» на порт 7778 (широкомовно й на broadcast кожної підмережі),
    /// клієнти слухають 7778 і збирають список кімнат. Гра йде на 7777. Без потоків: сокети неблокуючі, опитуються з NetDriver.Tick.
    /// </summary>
    public static class LanPorts
    {
        public const ushort Beacon = 7778;
        public const ushort Game = 7777;
    }

    /// <summary>MulticastLock на Android: без нього багато телефонів не віддають застосунку broadcast-пакети. Поза Android — нічого не робить.</summary>
    public static class AndroidMulticast
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _lock;
#endif

        public static void Acquire()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_lock != null && _lock.Call<bool>("isHeld"))
                {
                    return;
                }

                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    _lock = wifi.Call<AndroidJavaObject>("createMulticastLock", "banda");
                    _lock.Call("setReferenceCounted", false);
                    _lock.Call("acquire");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("MulticastLock: " + e.Message);
            }
#endif
        }

        public static void Release()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_lock != null && _lock.Call<bool>("isHeld"))
                {
                    _lock.Call("release");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("MulticastLock release: " + e.Message);
            }

            _lock = null;
#endif
        }
    }

    /// <summary>Передавач маяка (сторона хоста).</summary>
    public sealed class LanBeaconSender : IDisposable
    {
        public const float IntervalSeconds = 1f;
        private readonly UdpClient _udp;
        private readonly Func<BeaconInfo> _info;
        private readonly List<IPAddress> _targets = new List<IPAddress>();
        private float _timer = IntervalSeconds; // перший маяк — одразу
        private bool _disposed;

        public LanBeaconSender(Func<BeaconInfo> info)
        {
            _info = info;
            _udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            AndroidMulticast.Acquire();
            RefreshTargets();
            NetDriver.Instance.Tick += OnTick;
        }

        private void RefreshTargets()
        {
            _targets.Clear();
            _targets.Add(IPAddress.Broadcast);
            _targets.Add(IPAddress.Loopback); // той самий пристрій (тести, ПК)
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (UnicastIPAddressInformation ua in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null)
                        {
                            continue;
                        }

                        byte[] ip = ua.Address.GetAddressBytes();
                        byte[] mask = ua.IPv4Mask.GetAddressBytes();
                        byte[] b = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            b[i] = (byte)(ip[i] | ~mask[i]);
                        }

                        _targets.Add(new IPAddress(b));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("LAN-інтерфейси: " + e.Message); // лишається широкомовна адреса
            }
        }

        private void OnTick(float dt)
        {
            if (_disposed)
            {
                return;
            }

            _timer += dt;
            if (_timer < IntervalSeconds)
            {
                return;
            }

            _timer = 0f;
            byte[] data = Encoding.UTF8.GetBytes(BeaconCodec.Encode(_info()));
            foreach (IPAddress target in _targets)
            {
                try
                {
                    _udp.Send(data, data.Length, new IPEndPoint(target, LanPorts.Beacon));
                }
                catch (SocketException)
                {
                    // Інтерфейс зник чи мережа недоступна: наступний маяк за секунду.
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            NetDriver.Instance.Tick -= OnTick;
            _udp.Close();
            AndroidMulticast.Release();
        }
    }

    /// <summary>Слухач маяків (сторона клієнта): веде LanRoomList. Порт відкривається з ReuseAddress — на одному ПК можуть жити хост і клієнт.</summary>
    public sealed class LanBeaconListener : IDisposable
    {
        private readonly UdpClient _udp;
        private float _now;
        private bool _disposed;

        public LanBeaconListener()
        {
            AndroidMulticast.Acquire();
            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, LanPorts.Beacon));
            Rooms = new LanRoomList();
            NetDriver.Instance.Tick += OnTick;
        }

        public LanRoomList Rooms { get; }

        /// <summary>Спробувати відкрити слухача; на помилку (порт зайнятий недозволеним способом) повертає null і причину.</summary>
        public static LanBeaconListener TryCreate(out string error)
        {
            error = null;
            try
            {
                return new LanBeaconListener();
            }
            catch (SocketException e)
            {
                error = "Не вдалося слухати мережу: " + e.Message;
                return null;
            }
        }

        private void OnTick(float dt)
        {
            if (_disposed)
            {
                return;
            }

            _now += dt;
            try
            {
                while (_udp.Available > 0)
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp.Receive(ref from);
                    if (BeaconCodec.TryParse(Encoding.UTF8.GetString(data), out BeaconInfo info))
                    {
                        // З loopback-копії маяка лишаємо 127.0.0.1: так на одному ПК кімната знаходиться без реальної мережі.
                        Rooms.Seen(info, from.Address.ToString(), _now);
                    }
                }
            }
            catch (SocketException)
            {
                // Мережа зникла посеред прийому: список сам спорожніє за 3,5 с.
            }

            Rooms.Prune(_now);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            NetDriver.Instance.Tick -= OnTick;
            _udp.Close();
            AndroidMulticast.Release();
        }
    }
}
