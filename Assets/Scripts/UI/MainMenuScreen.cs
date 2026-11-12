using Banda.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.UI
{
    /// <summary>Головне меню: ім'я гравця, створити кімнату, приєднатися, тест датчиків.</summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _nameEdit;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _joinButton;
        [SerializeField] private Button _sensorsButton;
        [SerializeField] private TMP_Text _nameTitle;
        [SerializeField] private TMP_Text _versionLabel;

        private void OnEnable()
        {
            GameState state = GameState.Instance;
            if (state != null)
            {
                _nameEdit.SetTextWithoutNotify(state.MyName);
            }

            _nameEdit.onValueChanged.AddListener(OnNameChanged);
            _hostButton.onClick.AddListener(OnHostPressed);
            _joinButton.onClick.AddListener(OnJoinPressed);
            _sensorsButton.onClick.AddListener(OnSensorsPressed);
            _versionLabel.text = $"v{Application.version} · етап 1";
            UpdateButtons();
        }

        private void OnDisable()
        {
            _nameEdit.onValueChanged.RemoveListener(OnNameChanged);
            _hostButton.onClick.RemoveListener(OnHostPressed);
            _joinButton.onClick.RemoveListener(OnJoinPressed);
            _sensorsButton.onClick.RemoveListener(OnSensorsPressed);
        }

        private void OnNameChanged(string _)
        {
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool hasName = !string.IsNullOrWhiteSpace(_nameEdit.text);
            _hostButton.interactable = hasName;
            _joinButton.interactable = hasName;
            // Вимкнені кнопки пояснюють себе: чого бракує, щоб почати.
            // Підказка — у заголовку поля: панель не змінює розмір між станами.
            _nameTitle.text = hasName ? "ТВОЄ ІМ'Я" : "! БРАКУЄ ІМЕНІ";
            _nameTitle.color = hasName ? Palette.Steel2 : Palette.Neon3;
        }

        private void CommitName()
        {
            GameState.Instance?.SetMyName(_nameEdit.text);
        }

        private void OnHostPressed()
        {
            CommitName();
            GameState.Instance?.HostRoom(); // далі GameState переведе в лобі, а Router покаже екран лобі
        }

        private void OnJoinPressed()
        {
            CommitName();
            ScreenRouter.Instance?.ShowJoin();
        }

        private void OnSensorsPressed()
        {
            // НЕ ПЕРЕНЕСЕНО: екран тесту датчиків (sensor_test) — окремий крок перенесення.
            ScreenRouter.Instance?.Toast("Тест датчиків ще не перенесено в Unity");
        }
    }
}
