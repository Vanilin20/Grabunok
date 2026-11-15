using System;
using System.Collections;
using UnityEngine;

namespace Banda.Net
{
    /// <summary>Один постійний обʼєкт мережі: тік хоста, відкладені дії й корутини. Живе між сценами.</summary>
    public sealed class NetDriver : MonoBehaviour
    {
        private static NetDriver _instance;

        public static NetDriver Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("BandaNetDriver");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<NetDriver>();
                }

                return _instance;
            }
        }

        /// <summary>Тік кожен кадр з обмеженим кроком часу (затримка кадру не має ламати таймаути хоста).</summary>
        public event Action<float> Tick;

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            Tick?.Invoke(dt);
        }

        public static void After(float seconds, Action action)
        {
            Instance.StartCoroutine(Run(seconds, action));
        }

        private static IEnumerator Run(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action?.Invoke();
        }
    }
}
