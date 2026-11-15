using System;
using System.Threading.Tasks;

namespace Banda.Net
{
    /// <summary>Wi-Fi: хост оголошує кімнату UDP-маяком, клієнт знаходить її за кодом із 4 цифр. Запасний шлях — ручний IP.</summary>
    public sealed class LanRoomService : IRoomService
    {
        public const float SearchSeconds = 4f;
        private readonly Random _random = new Random();

        public RoomKind Kind => RoomKind.Lan;

        public Task<ServiceResult<HostSession>> CreateRoomAsync(string hostName, string token, string forcedCode = null)
        {
            string code = forcedCode ?? RoomCode.Generate(_random);
            HostSession session = HostSession.Start(LanPorts.Game, code, hostName, token, out string error);
            if (session == null)
            {
                return Task.FromResult(ServiceResult<HostSession>.Fail(error));
            }

            LanBeaconSender beacon;
            try
            {
                beacon = new LanBeaconSender(() => new BeaconInfo
                {
                    Code = code,
                    HostName = hostName,
                    Players = session.Room.SeatCount,
                    Open = session.Room.Phase == RoomPhase.Lobby && !session.Room.IsClosed,
                });
            }
            catch (System.Net.Sockets.SocketException e)
            {
                session.Dispose();
                return Task.FromResult(ServiceResult<HostSession>.Fail("Не вдалося оголосити кімнату в мережі: " + e.Message));
            }

            session.Attach(beacon);
            return Task.FromResult(ServiceResult<HostSession>.Success(session));
        }

        public async Task<ServiceResult<ClientSession>> JoinRoomAsync(string code, string playerName, string token)
        {
            if (!RoomCode.IsValid(code))
            {
                return ServiceResult<ClientSession>.Fail("Код кімнати — чотири цифри.");
            }

            LanBeaconListener listener = LanBeaconListener.TryCreate(out string error);
            if (listener == null)
            {
                return ServiceResult<ClientSession>.Fail(error);
            }

            LanRoom room = null;
            try
            {
                float waited = 0f;
                while (waited < SearchSeconds && !listener.Rooms.TryGet(code, out room))
                {
                    await Task.Delay(100);
                    waited += 0.1f;
                }
            }
            finally
            {
                listener.Dispose();
            }

            if (room == null)
            {
                return ServiceResult<ClientSession>.Fail("Кімнату " + code + " не знайдено в цій Wi-Fi. Перевір код і що телефони в одній мережі.");
            }

            if (!room.Info.Open)
            {
                return ServiceResult<ClientSession>.Fail("Справа в цій кімнаті вже почалась.");
            }

            return await JoinByIpAsync(room.Address, playerName, token);
        }

        /// <summary>Запасний шлях без пошуку: підключитись до хоста за IP (публічні Wi-Fi без broadcast, точка доступу).</summary>
        public Task<ServiceResult<ClientSession>> JoinByIpAsync(string ip, string playerName, string token)
        {
            if (!System.Net.IPAddress.TryParse(ip, out _))
            {
                return Task.FromResult(ServiceResult<ClientSession>.Fail("Це не схоже на IP-адресу. Приклад: 192.168.0.12"));
            }

            ClientSession session = ClientSession.Connect(ip, LanPorts.Game, token, playerName);
            return JoinWait.UntilJoined(session);
        }
    }
}
