using System.Collections;
using Banda.Core;
using TMPro;
using UnityEngine;

namespace Banda.UI
{
    /// <summary>
    /// Навігація між екранами й короткі сповіщення поверх будь-якого екрана (аналог Router з Godot).
    /// Екрани не знають один про одного: фазу гри міняє GameState, а Router вмикає відповідну панель.
    /// Усі екрани живуть в одній сцені — фон-місто не перезавантажується між ними.
    /// </summary>
    public sealed class ScreenRouter : MonoBehaviour
    {
        private const float ToastTime = 2.8f;
        private const float ToastFade = 0.4f;

        public static ScreenRouter Instance { get; private set; }

        [SerializeField] private GameObject _menuScreen;
        [SerializeField] private GameObject _joinScreen;
        [SerializeField] private GameObject _lobbyScreen;
        [SerializeField] private GameObject _heistScreen;
        [SerializeField] private GameObject _resultScreen;
        [SerializeField] private CanvasGroup _toastGroup;
        [SerializeField] private TMP_Text _toastLabel;

        private Coroutine _toastRoutine;

        private void Awake()
        {
            Instance = this;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // екран не гасне посеред справи
            if (_toastGroup != null)
            {
                _toastGroup.alpha = 0f;
                _toastGroup.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            GameState state = GameState.Instance;
            if (state == null)
            {
                Debug.LogError("[ScreenRouter] На сцені немає GameState — екрани не перемикатимуться.");
                return;
            }

            state.PhaseChanged += OnPhaseChanged;
            state.Notice += Toast;
        }

        private void OnDisable()
        {
            GameState state = GameState.Instance;
            if (state != null)
            {
                state.PhaseChanged -= OnPhaseChanged;
                state.Notice -= Toast;
            }
        }

        private void Start()
        {
            GameState state = GameState.Instance;
            ShowOnly(ScreenFor(state != null ? state.Phase : GamePhase.Menu));
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ShowMenu() => ShowOnly(_menuScreen);

        public void ShowJoin() => ShowOnly(_joinScreen);

        public void Toast(string text)
        {
            if (_toastGroup == null || _toastLabel == null)
            {
                return;
            }

            _toastLabel.text = text;
            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
            }

            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _toastGroup.gameObject.SetActive(true);
            _toastGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(ToastTime);
            float t = 0f;
            while (t < ToastFade)
            {
                t += Time.unscaledDeltaTime;
                _toastGroup.alpha = 1f - t / ToastFade;
                yield return null;
            }

            _toastGroup.alpha = 0f;
            _toastGroup.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            ShowOnly(ScreenFor(phase));
        }

        private GameObject ScreenFor(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Lobby:
                    return _lobbyScreen;
                case GamePhase.Heist:
                    return _heistScreen;
                case GamePhase.Result:
                    return _resultScreen;
                default:
                    return _menuScreen;
            }
        }

        private void ShowOnly(GameObject target)
        {
            GameObject[] all = { _menuScreen, _joinScreen, _lobbyScreen, _heistScreen, _resultScreen };
            foreach (GameObject screen in all)
            {
                if (screen != null && screen != target)
                {
                    screen.SetActive(false);
                }
            }

            if (target != null)
            {
                target.SetActive(true);
            }
        }
    }
}
