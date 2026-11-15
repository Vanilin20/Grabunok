using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Banda.Net
{
    /// <summary>
    /// Онлайн: сесія сервісів Unity (Lobby + Relay). Гравці нічого не реєструють — анонімний вхід. Кімната має код із 4 цифр
    /// (властивість сесії з індексом): за ним клієнт знаходить сесію й заходить. Довгий код сесії (6 символів) теж приймається.
    /// Хост — телефон гравця; вихід хоста закриває сесію. Міграції хоста немає.
    /// </summary>
    public sealed class RelayRoomService : IRoomService
    {
        public const float StepTimeoutSeconds = 20f;
        private const string CodeKey = "banda_code";
        private readonly string _profile;

        /// <param name="profile">Профіль автентифікації: боти на одному ПК мають різні профілі, інакше в усіх однаковий Player ID.</param>
        public RelayRoomService(string profile = null)
        {
            _profile = profile;
        }

        public RoomKind Kind => RoomKind.Relay;

        private static async Task<T> WithTimeout<T>(Task<T> task, float seconds)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds)));
            if (finished != task)
            {
                throw new TimeoutException();
            }

            return await task;
        }

        private static async Task WithTimeout(Task task, float seconds)
        {
            Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds)));
            if (finished != task)
            {
                throw new TimeoutException();
            }

            await task;
        }

        /// <summary>Ініціалізація сервісів і анонімний вхід (один раз за запуск). null — усе гаразд, інакше людське пояснення.</summary>
        private async Task<string> EnsureSignedIn()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                return RelayErrors.NoInternet;
            }

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    InitializationOptions options = new InitializationOptions();
                    if (!string.IsNullOrEmpty(_profile))
                    {
                        options.SetProfile(_profile);
                    }

                    await WithTimeout(UnityServices.InitializeAsync(options), StepTimeoutSeconds);
                }
                else if (UnityServices.State == ServicesInitializationState.Initializing)
                {
                    float waited = 0f;
                    while (UnityServices.State == ServicesInitializationState.Initializing && waited < StepTimeoutSeconds)
                    {
                        await Task.Delay(100);
                        waited += 0.1f;
                    }
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await WithTimeout(AuthenticationService.Instance.SignInAnonymouslyAsync(), StepTimeoutSeconds);
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Relay вхід: " + e);
                return RelayErrors.Describe(e);
            }
        }

        public async Task<ServiceResult<HostSession>> CreateRoomAsync(string hostName, string token, string forcedCode = null)
        {
            string authError = await EnsureSignedIn();
            if (authError != null)
            {
                return ServiceResult<HostSession>.Fail(authError);
            }

            System.Random random = new System.Random();
            try
            {
                string code = forcedCode;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    code = forcedCode ?? RoomCode.Generate(random);
                    if (await FindByCode(code) == null)
                    {
                        break;
                    }

                    if (forcedCode != null || attempt == 5)
                    {
                        return ServiceResult<HostSession>.Fail("Не вдалося підібрати вільний код кімнати. Спробуй ще раз.");
                    }
                }

                NetRuntime.CreateManager(out _); // сесія візьме NetworkManager.Singleton і сама налаштує Relay-транспорт
                SessionOptions options = new SessionOptions
                {
                    MaxPlayers = RoomHost.MaxPlayers,
                    IsPrivate = false, // потрібна для пошуку за 4-значним кодом
                }.WithRelayNetwork();
                options.SessionProperties[CodeKey] = new SessionProperty(code, VisibilityPropertyOptions.Public, PropertyIndex.String1);
                IHostSession session = await WithTimeout(MultiplayerService.Instance.CreateSessionAsync(options), StepTimeoutSeconds + 10f);
                HostSession host = HostSession.StartOnRunningManager(NetworkManager.Singleton, code, hostName, token);
                host.AttachLate(new SessionHandle(session, true));
                return ServiceResult<HostSession>.Success(host);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Relay створення: " + e);
                NetRuntime.DestroyManager(true);
                return ServiceResult<HostSession>.Fail(RelayErrors.Describe(e));
            }
        }

        private static async Task<ISessionInfo> FindByCode(string code)
        {
            QuerySessionsOptions query = new QuerySessionsOptions { Count = 5 };
            query.FilterOptions.Add(new FilterOption(FilterField.StringIndex1, code, FilterOperation.Equal));
            QuerySessionsResults results = await WithTimeout(MultiplayerService.Instance.QuerySessionsAsync(query), StepTimeoutSeconds);
            return results.Sessions.FirstOrDefault(s => s.AvailableSlots > 0);
        }

        public async Task<ServiceResult<ClientSession>> JoinRoomAsync(string code, string playerName, string token)
        {
            if (string.IsNullOrEmpty(code))
            {
                return ServiceResult<ClientSession>.Fail("Введи код кімнати.");
            }

            string authError = await EnsureSignedIn();
            if (authError != null)
            {
                return ServiceResult<ClientSession>.Fail(authError);
            }

            try
            {
                ISession session;
                NetRuntime.CreateManager(out _);
                if (RoomCode.IsValid(code))
                {
                    ISessionInfo info = await FindByCode(code);
                    if (info == null)
                    {
                        NetRuntime.DestroyManager(true);
                        return ServiceResult<ClientSession>.Fail("Кімнату " + code + " не знайдено онлайн. Перевір код: можливо, хост уже вийшов або кімната повна.", true);
                    }

                    string id = info.Id;
                    session = await WithTimeout(MultiplayerService.Instance.JoinSessionByIdAsync(id), StepTimeoutSeconds + 10f);
                    return await JoinWait.UntilJoined(Wrap(session, playerName, token, () => MultiplayerService.Instance.JoinSessionByIdAsync(id)));
                }

                string sessionCode = code.Trim().ToUpperInvariant();
                session = await WithTimeout(MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode), StepTimeoutSeconds + 10f);
                string sid = session.Id;
                return await JoinWait.UntilJoined(Wrap(session, playerName, token, () => MultiplayerService.Instance.JoinSessionByIdAsync(sid)));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Relay приєднання: " + e);
                NetRuntime.DestroyManager(true);
                return ServiceResult<ClientSession>.Fail(RelayErrors.Describe(e));
            }
        }

        private static ClientSession Wrap(ISession first, string playerName, string token, Func<Task<ISession>> rejoin)
        {
            SessionHandle handle = new SessionHandle(first, false);
            Func<Task<NetworkManager>> reconnect = async () =>
            {
                // Старе з'єднання зникло: виходимо зі старої сесії (якщо ще є) і заходимо знову; токен гравця той самий.
                handle.Dispose();
                await Task.Delay(300);
                NetRuntime.CreateManager(out _);
                ISession again = await WithTimeout(rejoin(), StepTimeoutSeconds + 10f);
                handle.Replace(again);
                return NetworkManager.Singleton;
            };
            ClientSession client = ClientSession.OnRunningManager(NetworkManager.Singleton, reconnect, token, playerName);
            client.Attach(handle);
            return client;
        }

        /// <summary>Тримач сесії сервісів: знищення = DeleteAsync (хост) або LeaveAsync (гравець); помилки виходу не важливі.</summary>
        private sealed class SessionHandle : IDisposable
        {
            private ISession _session;
            private readonly bool _isHost;

            public SessionHandle(ISession session, bool isHost)
            {
                _session = session;
                _isHost = isHost;
            }

            public void Replace(ISession session) => _session = session;

            public async void Dispose()
            {
                ISession s = _session;
                _session = null;
                if (s == null)
                {
                    return;
                }

                try
                {
                    if (_isHost && s is IHostSession hostSession)
                    {
                        await hostSession.DeleteAsync();
                    }
                    else
                    {
                        await s.LeaveAsync();
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("Вихід із сесії: " + e.Message);
                }
            }
        }
    }
}
