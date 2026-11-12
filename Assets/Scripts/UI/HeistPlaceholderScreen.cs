using Banda.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>
    /// ЗАГЛУШКА (явна): екран справи. Ролі (Хакер, Тінь, Зломщик) і симуляція пограбування
    /// ще не перенесені з Godot — це крок 3 перенесення. Зараз екран лише дає завершити справу
    /// з потрібним результатом, щоб перевірити повний цикл меню → лобі → справа → підсумок.
    /// </summary>
    public sealed class HeistPlaceholderScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _roleLabel;
        [SerializeField] private Button _successButton;
        [SerializeField] private Button _caughtButton;
        [SerializeField] private Button _timeoutButton;

        private void OnEnable()
        {
            GameState state = GameState.Instance;
            if (state != null)
            {
                _roleLabel.text = $"Твоя роль: {Roles.DisplayName(state.MyRole).ToUpperInvariant()}";
                _roleLabel.color = Roles.ColorOf(state.MyRole);
            }

            _successButton.onClick.AddListener(OnSuccess);
            _caughtButton.onClick.AddListener(OnCaught);
            _timeoutButton.onClick.AddListener(OnTimeout);
        }

        private void OnDisable()
        {
            _successButton.onClick.RemoveListener(OnSuccess);
            _caughtButton.onClick.RemoveListener(OnCaught);
            _timeoutButton.onClick.RemoveListener(OnTimeout);
        }

        private void OnSuccess() => GameState.Instance?.FinishHeist(HeistOutcome.Success);

        private void OnCaught() => GameState.Instance?.FinishHeist(HeistOutcome.Caught);

        private void OnTimeout() => GameState.Instance?.FinishHeist(HeistOutcome.Timeout);
    }
}
