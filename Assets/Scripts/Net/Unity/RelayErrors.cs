using System;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Banda.Net
{
    /// <summary>Помилки сервісів Unity — людською мовою: гравець бачить, що робити, а не код винятку.</summary>
    public static class RelayErrors
    {
        public const string NoInternet = "Немає інтернету. Перевір Wi-Fi чи мобільні дані й спробуй ще раз.";
        public const string NotLinked = "Онлайн-режим ще не налаштовано: проєкт не прив'язано до Unity Cloud. Це робить розробник; грайте поруч (Wi-Fi).";

        public static string Describe(Exception e)
        {
            if (e is AggregateException agg && agg.InnerException != null)
            {
                return Describe(agg.InnerException);
            }

            if (e is TimeoutException || e is TaskCanceledException || e is OperationCanceledException)
            {
                return "Сервер не відповів вчасно. Спробуй ще раз.";
            }

            if (e is ServicesInitializationException)
            {
                return NotLinked;
            }

            if (e is HttpRequestException || e is System.Net.WebException || e is System.Net.Sockets.SocketException)
            {
                return NoInternet;
            }

            if (e is SessionException se)
            {
                switch (se.Error)
                {
                    case SessionError.SessionNotFound:
                    case SessionError.SessionDeleted:
                        return "Кімнату не знайдено. Перевір код: можливо, хост уже вийшов.";
                    case SessionError.RateLimitExceeded:
                        return "Забагато спроб поспіль. Зачекай хвилину.";
                    case SessionError.NotAuthorized:
                    case SessionError.Forbidden:
                        return "Сервіс не пустив: вхід не вдався. Спробуй перезапустити гру.";
                    case SessionError.NetworkManagerStartFailed:
                        return "Не вдалося встановити з'єднання з хостом. Спробуй ще раз.";
                    default:
                        return InnerOrGeneric(se);
                }
            }

            if (e is RequestFailedException rfe)
            {
                switch (rfe.ErrorCode)
                {
                    case CommonErrorCodes.TransportError:
                        return NoInternet;
                    case CommonErrorCodes.Timeout:
                        return "Сервер не відповів вчасно. Спробуй ще раз.";
                    case CommonErrorCodes.TooManyRequests:
                        return "Забагато запитів до сервера. Зачекай хвилину.";
                    case CommonErrorCodes.ServiceUnavailable:
                        return "Сервіс тимчасово недоступний. Спробуй за кілька хвилин.";
                    case CommonErrorCodes.NotFound:
                        return "Кімнату не знайдено. Перевір код.";
                    case CommonErrorCodes.Forbidden:
                    case CommonErrorCodes.InvalidToken:
                    case CommonErrorCodes.TokenExpired:
                        return "Сервіс не пустив: вхід не вдався. Спробуй перезапустити гру.";
                    default:
                        return "Помилка сервісу (код " + rfe.ErrorCode + "). Спробуй ще раз.";
                }
            }

            return InnerOrGeneric(e);
        }

        private static string InnerOrGeneric(Exception e)
        {
            return e.InnerException != null ? Describe(e.InnerException) : "Щось пішло не так з онлайн-підключенням. Спробуй ще раз.";
        }
    }
}
