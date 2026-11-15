using System;
using System.Collections;
using System.Collections.Generic;
using Banda.Core;
using Banda.Heist;
using UnityEngine;

namespace Banda.Net
{
    /// <summary>
    /// Мережевий бот для автотестів: окремий процес збірки з аргументами «-bot host|shadow|safe» (див. tools/net_test.sh).
    /// Кожен бот друкує PASS/FAIL по перевірках і завершується з кодом 0 (усе гаразд) чи 1. Працює на справжньому NGO/UnityTransport.
    /// У редакторі й звичайній грі нічого не робить: без прапорця -bot метод одразу виходить.
    /// </summary>
    public static class NetBot
    {
        private static readonly List<string> _failures = new List<string>();
        private static string _name = "bot";
        private static ushort _port = NetRuntime.GamePort;
        private static string _ip = "127.0.0.1";
        private static string _roomCode = "4242";
        private static int _exitCode = -1;
        private static bool _foundByCode;
        private static string _profile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            string role = Arg(args, "-bot");
            if (role == null)
            {
                return;
            }

            _name = role;
            string p = Arg(args, "-botport");
            if (p != null && ushort.TryParse(p, out ushort port))
            {
                _port = port;
            }

            _ip = Arg(args, "-botip") ?? _ip;
            _roomCode = Arg(args, "-botcode") ?? _roomCode;
            _relay = Arg(args, "-botmode") == "relay";
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            IEnumerator body;
            switch (role)
            {
                case "host": body = HostBot(); break;
                case "shadow": body = ShadowBot(); break;
                case "safe": body = SafeBot(); break;
                default:
                    Log("FAIL невідома роль бота: " + role);
                    Application.Quit(1);
                    return;
            }

            NetDriver.Instance.StartCoroutine(Guard(body));
        }

        private static string Arg(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 0; i++)
            {
                if (args[i] == key)
                {
                    return i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : string.Empty;
                }
            }

            return null;
        }

        private static void Log(string line)
        {
            Debug.Log("[bot:" + _name + "] " + line);
            Console.WriteLine("[bot:" + _name + "] " + line);
        }

        private static void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                Log("PASS " + name);
            }
            else
            {
                _failures.Add(name);
                Log("FAIL " + name + (detail.Length > 0 ? " — " + detail : string.Empty));
            }
        }

        private static IEnumerator Guard(IEnumerator body)
        {
            float deadline = Time.realtimeSinceStartup + 90f;
            bool done = false;
            NetDriver.Instance.StartCoroutine(Wrap(body, () => done = true));
            while (!done && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!done)
            {
                Check("загальний тайм-аут 90 с", false);
            }

            Log(_failures.Count == 0 ? "РЕЗУЛЬТАТ: PASS" : "РЕЗУЛЬТАТ: FAIL (" + string.Join(", ", _failures) + ")");
            _exitCode = _failures.Count == 0 ? 0 : 1;
            // Дати останнім пакетам піти (інші боти ще можуть читати), потім вийти.
            yield return new WaitForSecondsRealtime(1.0f);
            Application.Quit(_exitCode);
        }

        private static IEnumerator Wrap(IEnumerator body, Action done)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!body.MoveNext())
                    {
                        break;
                    }

                    current = body.Current;
                }
                catch (Exception e)
                {
                    Check("виняток у боті", false, e.GetType().Name + ": " + e.Message);
                    break;
                }

                yield return current;
            }

            done();
        }

        private static IEnumerator Until(Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    Check("очікування: " + what, false, "тайм-аут " + timeout + " с");
                    yield break;
                }

                yield return null;
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        /// <summary>Спостерігач за тим, що реально приходить клієнтові: які ролі в статиці й знімках.</summary>
        private sealed class Probe
        {
            public RoleStatic Static;
            public RoleSnapshot Snapshot;
            public int StaticCount;
            public readonly HashSet<RoleId> RolesSeen = new HashSet<RoleId>();

            public Probe(RoomClient client)
            {
                client.Transport.StaticReceived += s => { Static = s; StaticCount++; RolesSeen.Add(s.Role); };
                client.Transport.SnapshotReceived += s => { Snapshot = s; RolesSeen.Add(s.View.Role); };
            }
        }

        // ------------------------------------------------------------------ клієнти

        private static bool _relay;

        private static IRoomService MakeService(string profile)
        {
            return _relay ? (IRoomService)new RelayRoomService(profile) : new LanRoomService();
        }

        /// <summary>Одна спроба зайти в кімнату: пошук за кодом (маяк чи сесія Relay); у Wi-Fi-режимі запасний шлях — прямий IP.</summary>
        private static IEnumerator JoinOnce(IRoomService service, string token, string name, Action<ClientSession> result)
        {
            System.Threading.Tasks.Task<ServiceResult<ClientSession>> find = service.JoinRoomAsync(_roomCode, name, token);
            yield return Until(() => find.IsCompleted, 40f, "пошук кімнати за кодом");
            ClientSession session = find.IsCompleted && find.Result.Ok ? find.Result.Value : null;
            if (session != null)
            {
                Log("кімнату знайдено за кодом " + _roomCode + (_relay ? " через сервіс Relay" : " через маяк"));
                _foundByCode = true;
            }
            else
            {
                if (find.IsCompleted)
                {
                    Log("пошук за кодом не вдався: " + find.Result.Error);
                }

                if (_relay)
                {
                    result(null);
                    yield break;
                }

                session = ClientSession.Connect(_ip, _port, token, name);
                yield return Until(() => session.Client.State != ClientState.Connecting, 10f, "відповідь хоста");
                if (session.Client.State != ClientState.Joined)
                {
                    session.Dispose();
                    session = null;
                }
            }

            result(session);
        }

        private static IEnumerator ConnectAndJoin(string token, string name, RoleId role, Action<ClientSession> result)
        {
            IRoomService service = MakeService(_name); // профіль автентифікації — лише ASCII (shadow/safe)
            // Хост-бот може стартувати трохи пізніше: до 12 спроб.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                ClientSession session = null;
                yield return JoinOnce(service, token, name, s => session = s);
                if (session != null && session.Client.State == ClientState.Joined)
                {
                    session.Client.PickRole(role);
                    result(session);
                    yield break;
                }

                session?.Dispose();
                yield return Wait(1.0f);
            }

            Check("підключення до хоста", false, "12 спроб");
            result(null);
        }

        // ------------------------------------------------------------------ хост (Хакер)

        private static IEnumerator HostBot()
        {
            System.Threading.Tasks.Task<ServiceResult<HostSession>> create = MakeService("host").CreateRoomAsync("Хакер", "tok-hacker", _roomCode);
            yield return Until(() => create.IsCompleted, 10f, "створення кімнати");
            HostSession host = create.IsCompleted && create.Result.Ok ? create.Result.Value : null;
            Check("1. хост відкрив кімнату " + _roomCode + " і оголошує її (" + (_relay ? "Relay" : "маяк") + ")", host != null, create.IsCompleted ? create.Result.Error ?? string.Empty : string.Empty);
            if (host == null)
            {
                yield break;
            }

            RoomClient me = host.LocalClient;
            Probe probe = new Probe(me);
            yield return Until(() => me.State == ClientState.Joined, 5f, "місце хоста");
            me.PickRole(RoleId.Hacker);
            yield return Until(() => me.Lobby != null && me.Lobby.Players.Count >= 3 && me.Lobby.CanStart, 40f, "в лобі троє з ролями");
            Check("1. два клієнти знайшли кімнату й зайняли різні ролі", me.Lobby != null && me.Lobby.CanStart);
            if (_failures.Count > 0)
            {
                host.Dispose();
                yield break;
            }

            me.RequestStart();
            yield return Until(() => probe.Static != null && probe.Snapshot != null, 8f, "статика й знімок Хакера");
            float shadowX0 = host.Room.Session.Sim.ShadowPos.X;
            HackerStatic st = probe.Static as HackerStatic;
            Check("2. Хакер має схему (стіни, камери, сейф)", st != null && st.Walls.Count > 0 && st.Cameras.Count > 0);
            yield return Wait(1.5f);
            Check("2. Хакер отримує лише дані своєї ролі (без комбінації й позиції Тіні)", probe.RolesSeen.Count == 1 && probe.RolesSeen.Contains(RoleId.Hacker));

            // 6: камера вимикається після дії Хакера.
            me.Transport.Send(HeistAction.CameraOff(0));
            yield return Until(() => probe.Snapshot.View is HackerView hv && hv.CamerasOff.Count > 0 && hv.CamerasOff[0] > 0f, 5f, "камера вимкнена");
            Check("6. камера вимикається після дії Хакера", probe.Snapshot.View is HackerView v && v.CamerasOff[0] > 0f);

            // 6: помилка на сейфі піднімає тривогу (її робить бот зломщика); у спільному стані троє.
            HeistSim sim = host.Room.Session.Sim;
            yield return Until(() => sim.SafeMistakes >= 1, 30f, "помилка зломщика");
            yield return Wait(0.5f);
            Check("6. помилка на сейфі піднімає тривогу", sim.Alarm >= HeistSim.AlarmSafeMistake - 0.01f && probe.Snapshot.Shared.Alarm > 0f);
            Check("6. у спільному стані 3 гравці", probe.Snapshot.Shared.Players.Count == 3);

            // 5: після обриву й повернення зломщик відкриває сейф.
            yield return Until(() => sim.SafeOpen, 40f, "сейф відкрито після перепідключення");
            Check("5. сейф відкрито правильною комбінацією після обриву", sim.SafeOpen);
            Check("3. Тінь рухається за діями клієнта (хост бачить зсув)", Mathf.Abs(sim.ShadowPos.X - shadowX0) > 1f, shadowX0 + " → " + sim.ShadowPos.X);
            yield return Wait(3.0f);
            host.Dispose();
            yield return Wait(1.0f);
        }

        // ------------------------------------------------------------------ Тінь

        private static IEnumerator ShadowBot()
        {
            ClientSession session = null;
            yield return ConnectAndJoin("tok-shadow", "Тінь", RoleId.Shadow, s => session = s);
            if (session == null)
            {
                yield break;
            }

            RoomClient client = session.Client;
            Probe probe = new Probe(client);
            Check("1. Тінь знайшла кімнату за кодом (код кімнати) і зайняла роль", client.State == ClientState.Joined && _foundByCode);
            yield return Until(() => probe.Static != null && probe.Snapshot != null, 45f, "початок справи");
            ShadowStatic st = probe.Static as ShadowStatic;
            ShadowView view = probe.Snapshot != null ? probe.Snapshot.View as ShadowView : null;
            Check("3. Тінь має свою позицію й радіус огляду", st != null && view != null);
            if (view == null)
            {
                session.Dispose();
                yield break;
            }

            float startX = view.Position.X;
            client.Transport.Send(HeistAction.Move(new Vec2(1f, 0f)));
            yield return Wait(1.0f);
            client.Transport.Send(HeistAction.Move(new Vec2(0f, 0f)));
            ShadowView after = probe.Snapshot.View as ShadowView;
            Check("3. Тінь рухається", after != null && after.Position.X > startX + 1f, startX + " → " + after?.Position.X);
            Check("3. Тінь не отримує схеми, комбінації й стану камер (лише дані ролі Тіні)", probe.RolesSeen.Count == 1 && probe.RolesSeen.Contains(RoleId.Shadow));
            yield return Wait(12f); // лишається в справі, поки хост перевіряє решту
            session.Dispose();
        }

        // ------------------------------------------------------------------ Зломщик

        private static IEnumerator SafeBot()
        {
            ClientSession session = null;
            yield return ConnectAndJoin("tok-safe", "Зломщик", RoleId.Safecracker, s => session = s);
            if (session == null)
            {
                yield break;
            }

            RoomClient client = session.Client;
            Probe probe = new Probe(client);
            Check("1. Зломщик знайшов кімнату за кодом (код кімнати) і зайняв роль", client.State == ClientState.Joined && _foundByCode);
            yield return Until(() => probe.Static != null && probe.Snapshot != null, 45f, "початок справи");
            SafecrackerStatic st = probe.Static as SafecrackerStatic;
            Check("4. Зломщик має комбінацію", st != null && st.Combo.Length == HeistSim.ComboLength);
            if (st == null)
            {
                session.Dispose();
                yield break;
            }

            int[] combo = (int[])st.Combo.Clone();
            int[] wrong = new int[combo.Length];
            for (int i = 0; i < combo.Length; i++)
            {
                wrong[i] = (combo[i] + 1) % st.Range;
            }

            client.Transport.Send(HeistAction.SafeTry(wrong));
            yield return Until(() => probe.Snapshot.View is SafecrackerView sv && sv.Mistakes == 1, 5f, "помилка зарахована");
            Check("4. хибна комбінація → помилка, сейф зачинений", probe.Snapshot.View is SafecrackerView v1 && v1.Mistakes == 1 && !v1.Open);
            Check("4. Зломщик не отримує схеми (лише дані ролі Зломщика)", probe.RolesSeen.Count == 1 && probe.RolesSeen.Contains(RoleId.Safecracker));

            // 5: обрив посеред справи.
            yield return Wait(1.0f);
            yield return Wait(0.5f);
            int mistakesBefore = (probe.Snapshot.View as SafecrackerView)?.Mistakes ?? -1;
            Probe oldProbe = probe;
            Log("обриваю з'єднання");
            session.Dispose();
            yield return Wait(1.5f);

            // Той самий токен, нове з'єднання: нова сесія → нова роль-клієнт із тим самим tok-safe.
            ClientSession back = null;
            RoomClient backClient = null;
            IRoomService service = MakeService("safe");
            for (int attempt = 0; attempt < 6 && back == null; attempt++)
            {
                ClientSession s2 = null;
                yield return JoinOnce(service, "tok-safe", "Зломщик", s => s2 = s);
                if (s2 != null && s2.Client.State == ClientState.Joined)
                {
                    back = s2;
                    backClient = s2.Client;
                }
                else
                {
                    s2?.Dispose();
                    yield return Wait(1.5f);
                }
            }

            Check("5. після обриву клієнт повернувся в кімнату", back != null);
            if (back == null)
            {
                yield break;
            }

            Probe p2 = new Probe(backClient);
            backClient.Transport.RequestState(); // статика могла прийти до підписки проби: екран після відкриття теж просить повний стан
            yield return Until(() => p2.Static != null && p2.Snapshot != null, 8f, "статика й знімок після повернення");
            Check("5. повернувся на ту саму роль (Resumed)", backClient.Welcome.Resumed && backClient.Role == RoleId.Safecracker);
            SafecrackerStatic st2 = p2.Static as SafecrackerStatic;
            Check("5. комбінацію прислано знову", st2 != null && st2.Combo.Length == combo.Length);
            SafecrackerView v2 = p2.Snapshot.View as SafecrackerView;
            Check("5. стан сейфа збережено (помилки не скинулись)", v2 != null && v2.Mistakes == mistakesBefore && mistakesBefore >= 1, "було " + mistakesBefore + ", стало " + v2?.Mistakes);
            backClient.Transport.Send(HeistAction.SafeTry(combo));
            yield return Until(() => p2.Snapshot.View is SafecrackerView sv && sv.Open, 5f, "сейф відкрито");
            Check("5. правильна комбінація відкриває сейф", p2.Snapshot.View is SafecrackerView v3 && v3.Open);
            yield return Wait(6f);
            back.Dispose();
        }
    }
}
