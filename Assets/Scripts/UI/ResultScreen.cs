using Banda.Core;
using Banda.Heist;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>
    /// Підсумок справи — газетна шпальта (ТЗ: «газетна шпальта про банду»): заголовок з того, що сталося,
    /// цифри справи в колонці, штамп «СЕНСАЦІЯ» чи «ПРОВАЛ». Трюки слави й гроші — етап 3 (ТЗ), тут їх немає.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _headline;
        [SerializeField] private TMP_Text _stats;
        [SerializeField] private Image _stamp;
        [SerializeField] private Sprite _stampWin;
        [SerializeField] private Sprite _stampFail;
        [SerializeField] private Button _againButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private TMP_Text _waitLabel;

        private void OnEnable()
        {
            GameState state = GameState.Instance;
            if (state == null)
            {
                return;
            }

            _againButton.onClick.AddListener(OnAgain);
            _leaveButton.onClick.AddListener(OnLeave);
            _againButton.gameObject.SetActive(state.IsHost);
            _waitLabel.gameObject.SetActive(!state.IsHost);
            state.ResultReceived += Show;
            Show(state.LastResult);
        }

        private void OnDisable()
        {
            _againButton.onClick.RemoveListener(OnAgain);
            _leaveButton.onClick.RemoveListener(OnLeave);
            if (GameState.Instance != null)
            {
                GameState.Instance.ResultReceived -= Show;
            }
        }

        private void OnAgain() => GameState.Instance?.BackToLobby();

        private void OnLeave() => GameState.Instance?.LeaveRoom();

        private void Show(HeistSummary summary)
        {
            bool win = summary.Outcome == HeistOutcome.Success;
            switch (summary.Outcome)
            {
                case HeistOutcome.Success:
                    _headline.text = summary.SafeMistakes == 0 && summary.MaxAlarm < 30
                        ? "ТИХО ЯК ТІНЬ: СЕЙФ МАГАЗИНУ ПОРОЖНІЙ"
                        : "БАНДА ВИНЕСЛА СЕЙФ МАГАЗИНУ НА РОЗІ";
                    break;
                case HeistOutcome.Caught:
                    _headline.text = "СИГНАЛІЗАЦІЯ ЗІРВАЛА НІЧНЕ ПОГРАБУВАННЯ";
                    break;
                case HeistOutcome.Timeout:
                    _headline.text = "РАНКОВА ЗМІНА ЗАСТАЛА ЗЛОДІЇВ НА ГАРЯЧОМУ";
                    break;
                default:
                    _headline.text = "…";
                    break;
            }

            _stamp.sprite = win ? _stampWin : _stampFail;
            _stamp.SetNativeSize();
            int seconds = summary.TimeUsedSeconds;
            _stats.text = $"ЧАС СПРАВИ: {seconds / 60}:{seconds % 60:00}\nМАКС. ТРИВОГА: {summary.MaxAlarm}%\n" +
                          $"ПОМИЛОК НА СЕЙФІ: {summary.SafeMistakes}\nЗДОБИЧ: {(summary.Loot ? "Є" : "НЕМАЄ")}";
        }
    }
}
