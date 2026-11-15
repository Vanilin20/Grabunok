using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Banda.Net
{
    /// <summary>Створення NetworkManager у коді (без сцени й префабів) із налаштуваннями під Банду.</summary>
    public static class NetRuntime
    {
        public const ushort GamePort = 7777;

        /// <summary>
        /// Новий NetworkManager + UnityTransport. Попередній (якщо був) знищується.
        /// Сцени, префаби й гравці NGO не використовуються: лише транспорт і іменовані повідомлення.
        /// </summary>
        public static NetworkManager CreateManager(out UnityTransport transport)
        {
            DestroyManager();
            GameObject go = new GameObject("BandaNetwork");
            go.SetActive(false); // NetworkManager.Awake читає конфіг — спершу налаштувати, потім увімкнути
            transport = go.AddComponent<UnityTransport>();
            NetworkManager nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ForceSamePrefabs = false,
                ConnectionApproval = false,
                EnableNetworkLogs = false,
            };
            // Обрив Wi-Fi / мобільної мережі має виявлятись за секунди, а не за 30 с: гравець побачить «зв'язок пропав» і повернеться.
            transport.DisconnectTimeoutMS = 8000;
            transport.ConnectTimeoutMS = 1500;
            transport.MaxConnectAttempts = 8;
            go.SetActive(true);
            Object.DontDestroyOnLoad(go);
            return nm;
        }

        public static void DestroyManager()
        {
            NetworkManager existing = NetworkManager.Singleton;
            if (existing == null)
            {
                return;
            }

            if (existing.IsListening)
            {
                existing.Shutdown();
            }

            Object.Destroy(existing.gameObject);
        }
    }
}
