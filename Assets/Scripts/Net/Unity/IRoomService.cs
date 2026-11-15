using System.Threading.Tasks;

namespace Banda.Net
{
    public enum RoomKind
    {
        /// <summary>Поруч: та сама Wi-Fi, UDP-маяк.</summary>
        Lan,

        /// <summary>Через інтернет: Unity Relay + сесія з кодом.</summary>
        Relay,

        /// <summary>Власний сервер (поки не зроблено): див. docs/network_options.md.</summary>
        Dedicated,
    }

    /// <summary>Результат дії сервісу кімнат: або значення, або людське пояснення українською.</summary>
    public sealed class ServiceResult<T> where T : class
    {
        private ServiceResult(T value, string error, bool notFound)
        {
            Value = value;
            Error = error;
            NotFound = notFound;
        }

        public T Value { get; }

        public string Error { get; }

        /// <summary>Кімнату з таким кодом не знайдено (на відміну від «знайдено, але не пустили» чи збою мережі).</summary>
        public bool NotFound { get; }

        public bool Ok => Value != null;

        public static ServiceResult<T> Success(T value) => new ServiceResult<T>(value, null, false);

        public static ServiceResult<T> Fail(string error, bool notFound = false) => new ServiceResult<T>(null, error, notFound);
    }

    /// <summary>
    /// Як гравці знаходять одне одного. Створення кімнати дає HostSession (код кімнати в HostSession.Room.Code), приєднання за кодом — ClientSession.
    /// Реалізації: LanRoomService (Wi-Fi), RelayRoomService (онлайн). Третя — власний сервер (Linux, headless-збірка з HeistSim): її сервіс
    /// лише знаходить адресу за кодом і повертає ClientSession.Connect(адреса, порт); хост-частина там — окремий процес (див. docs/network_options.md).
    /// </summary>
    public interface IRoomService
    {
        RoomKind Kind { get; }

        /// <summary>Створити кімнату й почати її оголошувати. forcedCode — лише для тестів (за замовчуванням код випадковий).</summary>
        Task<ServiceResult<HostSession>> CreateRoomAsync(string hostName, string token, string forcedCode = null);

        /// <summary>Знайти кімнату за кодом і підключитись; результат — клієнт у стані Joined (або зрозуміла причина невдачі).</summary>
        Task<ServiceResult<ClientSession>> JoinRoomAsync(string code, string playerName, string token);
    }

    /// <summary>Спільне очікування відповіді хоста для сервісів кімнат.</summary>
    public static class JoinWait
    {
        public const float TimeoutSeconds = 14f;

        /// <summary>Чекає, поки хост відповість на Hello. Повертає клієнт у стані Joined або людське пояснення.</summary>
        public static async Task<ServiceResult<ClientSession>> UntilJoined(ClientSession session, float timeout = TimeoutSeconds)
        {
            float waited = 0f;
            while (session.Client.State == ClientState.Connecting && waited < timeout)
            {
                await Task.Delay(100);
                waited += 0.1f;
            }

            switch (session.Client.State)
            {
                case ClientState.Joined:
                    return ServiceResult<ClientSession>.Success(session);
                case ClientState.Rejected:
                    string why = session.Client.StateMessage;
                    session.Dispose();
                    return ServiceResult<ClientSession>.Fail(why);
                default:
                    session.Dispose();
                    return ServiceResult<ClientSession>.Fail(session.StartError ?? "Не вдалося підключитись до хоста. Перевір, що ви в одній мережі й хост ще в кімнаті.");
            }
        }
    }
}
