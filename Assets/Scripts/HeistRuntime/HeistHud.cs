using System.Collections.Generic;
using Banda.Core;
using Banda.Heist;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Спільне для всіх ролей (ТЗ): шкала тривоги вгорі (20 сегментів, поріг 50% підписаний, після 50% пульсує),
    /// значення у відсотках, таймер, фішки гравців — іконка ролі й стан (зайнятий / у небезпеці / без зв'язку).
    /// Плюс панель «гарячого місця» 1-2-3, якщо ролі на одному телефоні.
    /// </summary>
    public sealed class HeistHud : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Image[] _segments = System.Array.Empty<Image>();
        [SerializeField] private Image _siren;
        [SerializeField] private TMP_Text _alarmValue;
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private Image[] _chipIcons = System.Array.Empty<Image>();
        [SerializeField] private Image[] _chipStatus = System.Array.Empty<Image>();
        [SerializeField] private Image[] _chipActive = System.Array.Empty<Image>();
        [SerializeField] private Sprite _iconHacker;
        [SerializeField] private Sprite _iconShadow;
        [SerializeField] private Sprite _iconSafecracker;
        [SerializeField] private Sprite _statusBusy;
        [SerializeField] private Sprite _statusDanger;
        [SerializeField] private Sprite _statusOffline;
        [SerializeField] private GameObject _hotSeatBar;
        [SerializeField] private Button[] _hotSeatButtons = System.Array.Empty<Button>();

        private ITransport _transport;
        private float _alarm;
        private bool _alert;
        private float _timeLeft;
        private float _pulse;

        private void Awake()
        {
            _root.SetActive(false);
            RoleId[] roles = HeistDirector.MvpRoles;
            for (int i = 0; i < _hotSeatButtons.Length && i < roles.Length; i++)
            {
                RoleId role = roles[i];
                _hotSeatButtons[i].onClick.AddListener(() => HeistDirector.Instance?.SwitchTo(role));
            }
        }

        private void OnEnable()
        {
            if (HeistDirector.Instance != null)
            {
                HeistDirector.Instance.HeistStarted += Attach;
                HeistDirector.Instance.HeistStopped += Detach;
            }
        }

        private void OnDisable()
        {
            if (HeistDirector.Instance != null)
            {
                HeistDirector.Instance.HeistStarted -= Attach;
                HeistDirector.Instance.HeistStopped -= Detach;
            }

            Detach();
        }

        private void Attach(ITransport transport)
        {
            Detach();
            _transport = transport;
            _transport.SnapshotReceived += OnSnapshot;
            _transport.LocalRoleChanged += OnRole;
            _root.SetActive(true);
            _hotSeatBar.SetActive(HeistDirector.Instance != null && HeistDirector.Instance.IsHotSeat);
            OnRole(transport.LocalRole);
        }

        private void Detach()
        {
            if (_transport != null)
            {
                _transport.SnapshotReceived -= OnSnapshot;
                _transport.LocalRoleChanged -= OnRole;
                _transport = null;
            }

            _root.SetActive(false);
        }

        private void OnRole(RoleId role)
        {
            RoleId[] roles = HeistDirector.MvpRoles;
            for (int i = 0; i < _hotSeatButtons.Length && i < roles.Length; i++)
            {
                Image back = _hotSeatButtons[i].targetGraphic as Image;
                if (back != null)
                {
                    back.color = roles[i] == role ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                }
            }
        }

        private void OnSnapshot(RoleSnapshot snapshot)
        {
            SharedView shared = snapshot.Shared;
            _alarm = shared.Alarm;
            _alert = shared.Alert;
            _timeLeft = shared.TimeLeft;
            int t = Mathf.CeilToInt(_timeLeft);
            _timer.text = $"{t / 60}:{t % 60:00}";
            _timer.color = _timeLeft < 60f ? Palette.Danger : Palette.Text;
            _alarmValue.text = $"{Mathf.RoundToInt(_alarm)}%";
            List<PlayerChip> players = shared.Players;
            for (int i = 0; i < _chipIcons.Length; i++)
            {
                bool has = i < players.Count;
                _chipIcons[i].transform.parent.gameObject.SetActive(has);
                if (!has)
                {
                    continue;
                }

                PlayerChip chip = players[i];
                _chipIcons[i].sprite = chip.Role == RoleId.Hacker ? _iconHacker : chip.Role == RoleId.Shadow ? _iconShadow : _iconSafecracker;
                Sprite status = !chip.Connected ? _statusOffline : chip.Status == PlayerStatus.Danger ? _statusDanger :
                    chip.Status == PlayerStatus.Busy ? _statusBusy : null;
                _chipStatus[i].sprite = status;
                _chipStatus[i].enabled = status != null;
                _chipStatus[i].SetNativeSize();
                _chipActive[i].enabled = _transport != null && chip.Role == _transport.LocalRole;
            }
        }

        private void Update()
        {
            if (!_root.activeSelf)
            {
                return;
            }

            // Сегменти: жовті до 50%, бурштинові до 80%, червоні далі. Після порогу шкала пульсує — читається без тексту.
            _pulse += Time.deltaTime * (_alert ? 6f : 0f);
            float pulse = _alert ? 0.75f + 0.25f * Mathf.Sin(_pulse) : 1f;
            for (int i = 0; i < _segments.Length; i++)
            {
                float t = (i + 1f) / _segments.Length;
                bool on = _alarm / 100f >= t - 0.5f / _segments.Length;
                Color c = t <= 0.5f ? Palette.Lamp : t <= 0.8f ? Palette.Lamp2 : Palette.Danger;
                _segments[i].color = on ? new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1f) : Palette.Night3;
            }

            _siren.color = _alert ? new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(_pulse)) : new Color(1f, 1f, 1f, 0.55f);
        }
    }
}
