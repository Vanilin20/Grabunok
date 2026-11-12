using System;
using System.Collections.Generic;
using Banda.Core;

namespace Banda.Heist
{
    /// <summary>
    /// Справа на хості: гравці (токен → ім'я, роль, зв'язок) + симуляція.
    /// Тут вирішується, ЧИЯ дія і ЩО кому віддати: дія приймається тільки від власника ролі,
    /// кожен гравець отримує знімок лише своєї ролі. Перепідключення — за токеном (як у Godot GameState):
    /// гравець повертається на ту саму роль, а симуляція не помічає обриву.
    /// Мережевий транспорт і LocalTransport користуються тим самим класом.
    /// </summary>
    public sealed class HeistSession
    {
        private sealed class Player
        {
            public string Token = string.Empty;
            public string Name = string.Empty;
            public RoleId Role = RoleId.None;
            public bool Connected = true;
        }

        private readonly Dictionary<string, Player> _players = new Dictionary<string, Player>();
        private readonly List<string> _order = new List<string>();

        public HeistSession(HeistLevel level, int seed)
        {
            Sim = new HeistSim(level, seed);
        }

        public HeistSim Sim { get; }

        public event Action<HeistSummary> Finished;

        public IReadOnlyList<string> Tokens => _order;

        /// <summary>Додати гравця. Одна роль — один гравець (ТЗ).</summary>
        public void AddPlayer(string token, string name, RoleId role)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Порожній токен гравця", nameof(token));
            }

            foreach (Player other in _players.Values)
            {
                if (other.Role == role && other.Token != token)
                {
                    throw new InvalidOperationException($"Роль {role} уже зайнята");
                }
            }

            if (!_players.ContainsKey(token))
            {
                _order.Add(token);
            }

            _players[token] = new Player { Token = token, Name = name ?? string.Empty, Role = role };
        }

        public RoleId RoleOf(string token) => token != null && _players.TryGetValue(token, out Player p) ? p.Role : RoleId.None;

        public bool IsConnected(string token) => token != null && _players.TryGetValue(token, out Player p) && p.Connected;

        /// <summary>Обрив зв'язку: роль лишається за гравцем, справа йде далі.</summary>
        public void MarkDisconnected(string token)
        {
            if (token != null && _players.TryGetValue(token, out Player p))
            {
                p.Connected = false;
            }
        }

        /// <summary>Повернення за тим самим токеном. Повертає роль (None — невідомий гравець).</summary>
        public RoleId Reconnect(string token)
        {
            if (token == null || !_players.TryGetValue(token, out Player p))
            {
                return RoleId.None;
            }

            p.Connected = true;
            return p.Role;
        }

        /// <summary>Дія від гравця. Роль бере хост із реєстру, а не з дії — підробити чужу дію не можна.</summary>
        public bool Submit(string token, HeistAction action)
        {
            if (token == null || !_players.TryGetValue(token, out Player p) || !p.Connected)
            {
                return false;
            }

            return Sim.Apply(p.Role, action);
        }

        public void Tick(float delta)
        {
            bool wasFinished = Sim.IsFinished;
            Sim.Tick(delta);
            if (!wasFinished && Sim.IsFinished)
            {
                Finished?.Invoke(Sim.Summary());
            }
        }

        public RoleStatic StaticFor(string token) => Sim.StaticViewFor(RoleOf(token));

        public RoleSnapshot SnapshotFor(string token)
        {
            return new RoleSnapshot { Shared = BuildShared(), View = Sim.ViewFor(RoleOf(token)) };
        }

        private SharedView BuildShared()
        {
            SharedView shared = new SharedView { Alarm = Sim.Alarm, TimeLeft = Sim.TimeLeft, Alert = Sim.Alert };
            foreach (string token in _order)
            {
                Player p = _players[token];
                shared.Players.Add(new PlayerChip
                {
                    Name = p.Name,
                    Role = p.Role,
                    Status = Sim.StatusFor(p.Role),
                    Connected = p.Connected,
                });
            }

            return shared;
        }
    }
}
