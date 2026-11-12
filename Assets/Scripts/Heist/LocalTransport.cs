using System;
using System.Collections.Generic;
using Banda.Core;

namespace Banda.Heist
{
    /// <summary>
    /// Справа цілком на одному пристрої. Хост і всі «гравці» живуть тут; знімки роздаються так само,
    /// як їх роздавала б мережа — кожній ролі тільки своє.
    ///
    /// Режим «гаряче місце» (для тестів самому): усі ролі на одному телефоні, SwitchRole перемикає,
    /// чий екран показано. Справа не зупиняється при перемиканні — як і за столом з трьома телефонами.
    ///
    /// Час рухає власник (MonoBehaviour на сцені) через Update(delta), тож клас тестується без рушія.
    /// </summary>
    public sealed class LocalTransport : ITransport
    {
        /// <summary>ТЗ: хост оновлює стан 10–20 разів на секунду.</summary>
        public const float SendRate = 15f;

        private readonly HeistSession _session;
        private readonly Dictionary<RoleId, string> _tokenByRole = new Dictionary<RoleId, string>();
        private float _sendAccumulator;
        private bool _finishedSent;

        public LocalTransport(HeistSession session, RoleId initialRole)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            foreach (string token in session.Tokens)
            {
                _tokenByRole[session.RoleOf(token)] = token;
            }

            if (!_tokenByRole.ContainsKey(initialRole))
            {
                throw new ArgumentException($"У справі немає гравця з роллю {initialRole}", nameof(initialRole));
            }

            LocalRole = initialRole;
            _session.Finished += OnFinished;
        }

        public RoleId LocalRole { get; private set; }

        public HeistSession Session => _session;

        public event Action<RoleStatic> StaticReceived;
        public event Action<RoleSnapshot> SnapshotReceived;
        public event Action<HeistSummary> Finished;
        public event Action<RoleId> LocalRoleChanged;

        /// <summary>Ролі, між якими можна перемикатися в «гарячому місці».</summary>
        public IEnumerable<RoleId> Roles => _tokenByRole.Keys;

        /// <summary>Перший знімок і статика — викликати, коли екрани підписались.</summary>
        public void Begin()
        {
            StaticReceived?.Invoke(_session.StaticFor(_tokenByRole[LocalRole]));
            SnapshotReceived?.Invoke(_session.SnapshotFor(_tokenByRole[LocalRole]));
        }

        public void Send(HeistAction action)
        {
            _session.Submit(_tokenByRole[LocalRole], action);
        }

        /// <summary>«Гаряче місце»: показати екран іншої ролі.</summary>
        public bool SwitchRole(RoleId role)
        {
            if (role == LocalRole || !_tokenByRole.ContainsKey(role))
            {
                return false;
            }

            // Тінь, що лишилась без «пальця», зупиняється — інакше вона бігла б далі сама.
            if (LocalRole == RoleId.Shadow)
            {
                Send(HeistAction.Move(Vec2.Zero));
            }

            LocalRole = role;
            LocalRoleChanged?.Invoke(role);
            Begin();
            return true;
        }

        public void Update(float delta)
        {
            _session.Tick(delta);
            _sendAccumulator += delta;
            if (_sendAccumulator >= 1f / SendRate)
            {
                _sendAccumulator = 0f;
                SnapshotReceived?.Invoke(_session.SnapshotFor(_tokenByRole[LocalRole]));
            }
        }

        private void OnFinished(HeistSummary summary)
        {
            if (_finishedSent)
            {
                return;
            }

            _finishedSent = true;
            SnapshotReceived?.Invoke(_session.SnapshotFor(_tokenByRole[LocalRole]));
            Finished?.Invoke(summary);
        }
    }
}
