using Banda.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>
    /// Підсумок справи.
    /// ЗАГЛУШКА (з ТЗ): газетна шпальта з трюками слави — етап 3. Зараз — сухі цифри, як і в Godot-версії.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _reason;
        [SerializeField] private TMP_Text _stats;
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
            switch (summary.Outcome)
            {
                case HeistOutcome.Success:
                    _title.text = "СПРАВУ ЗРОБЛЕНО";
                    _title.color = Palette.Safe;
                    _reason.text = "Здобич винесли";
                    break;
                case HeistOutcome.Caught:
                    _title.text = "ПРОВАЛ";
                    _title.color = Palette.Danger;
                    _reason.text = "Тривога 100% — спрацювала сигналізація";
                    break;
                case HeistOutcome.Timeout:
                    _title.text = "ПРОВАЛ";
                    _title.color = Palette.Danger;
                    _reason.text = "Прийшла ранкова зміна";
                    break;
                default:
                    _title.text = "…";
                    _reason.text = string.Empty;
                    break;
            }

            int seconds = summary.TimeUsedSeconds;
            _stats.text = $"Час: {seconds / 60}:{seconds % 60:00}\nМакс. тривога: {summary.MaxAlarm}%\nПомилок на сейфі: {summary.SafeMistakes}";
        }
    }
}
