using System;
using System.Collections.Generic;
using Banda.Core;
using Banda.Heist;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Запускає справу, коли GameState переходить у фазу Heist, і веде її час.
    /// Зараз — LocalTransport (усе на цьому пристрої). Мережевий хост підставить свій ITransport,
    /// а екрани ролей нічого не помітять: вони знають тільки Transport.
    ///
    /// «Гаряче місце»: якщо в кімнаті не всі три ролі MVP зайняті живими гравцями, відсутні ролі
    /// додаються як «місця» на цьому ж телефоні. Перемикання — клавіші 1-2-3 або кнопки HotSeatBar.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class HeistDirector : MonoBehaviour
    {
        public static readonly RoleId[] MvpRoles = { RoleId.Hacker, RoleId.Shadow, RoleId.Safecracker };

        public static HeistDirector Instance { get; private set; }

        /// <summary>Нова справа: транспорт готовий, екрани можуть підписуватись.</summary>
        public event Action<ITransport> HeistStarted;

        /// <summary>Справа закінчилась або перервана — екрани відписуються.</summary>
        public event Action HeistStopped;

        public ITransport Transport => _transport;

        public bool IsHotSeat { get; private set; }

        public IReadOnlyList<RoleId> HotSeatRoles => _hotSeatRoles;

        private LocalTransport _transport;
        private readonly List<RoleId> _hotSeatRoles = new List<RoleId>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (GameState.Instance != null)
            {
                GameState.Instance.PhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (GameState.Instance != null)
            {
                GameState.Instance.PhaseChanged -= OnPhaseChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Heist)
            {
                StartHeist();
            }
            else if (_transport != null)
            {
                StopHeist();
            }
        }

        private void StartHeist()
        {
            GameState state = GameState.Instance;
            // Сід справи — випадковий; у мережевій версії його знатиме лише хост.
            HeistSession session = new HeistSession(HeistLevel.CornerStore(), UnityEngine.Random.Range(1, int.MaxValue));
            RoleId myRole = RoleId.None;
            _hotSeatRoles.Clear();
            foreach (LobbyPlayer player in state.LobbyPlayers)
            {
                if (player.Role == RoleId.None)
                {
                    continue;
                }

                session.AddPlayer(player.IsMe ? "local" : "peer-" + player.Name, player.Name, player.Role);
                if (player.IsMe)
                {
                    myRole = player.Role;
                    _hotSeatRoles.Add(player.Role);
                }
            }

            // Ролі MVP, яких ніхто не взяв, — «гаряче місце» на цьому телефоні.
            foreach (RoleId role in MvpRoles)
            {
                if (!HasRole(session, role))
                {
                    session.AddPlayer("hotseat-" + role, Roles.DisplayName(role), role);
                    _hotSeatRoles.Add(role);
                }
            }

            IsHotSeat = _hotSeatRoles.Count > 1;
            if (myRole == RoleId.None)
            {
                myRole = _hotSeatRoles.Count > 0 ? _hotSeatRoles[0] : RoleId.Shadow;
            }

            _transport = new LocalTransport(session, myRole);
            _transport.Finished += OnFinished;
            HeistStarted?.Invoke(_transport);
            _transport.Begin();
        }

        private static bool HasRole(HeistSession session, RoleId role)
        {
            foreach (string token in session.Tokens)
            {
                if (session.RoleOf(token) == role)
                {
                    return true;
                }
            }

            return false;
        }

        private void StopHeist()
        {
            _transport.Finished -= OnFinished;
            _transport = null;
            HeistStopped?.Invoke();
        }

        private void OnFinished(HeistSummary summary)
        {
            GameState.Instance?.FinishHeist(summary);
        }

        /// <summary>«Гаряче місце»: перемкнути екран на іншу роль цього телефона.</summary>
        public void SwitchTo(RoleId role)
        {
            if (_transport != null && IsHotSeat && _hotSeatRoles.Contains(role))
            {
                _transport.SwitchRole(role);
            }
        }

        private void Update()
        {
            if (_transport == null)
            {
                return;
            }

            if (IsHotSeat)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame)
                    {
                        SwitchTo(RoleId.Hacker);
                    }
                    else if (keyboard.digit2Key.wasPressedThisFrame)
                    {
                        SwitchTo(RoleId.Shadow);
                    }
                    else if (keyboard.digit3Key.wasPressedThisFrame)
                    {
                        SwitchTo(RoleId.Safecracker);
                    }
                }
            }

            _transport.Update(Mathf.Min(Time.deltaTime, 0.1f));
        }
    }
}
