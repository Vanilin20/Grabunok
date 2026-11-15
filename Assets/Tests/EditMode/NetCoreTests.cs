using System;
using System.Collections.Generic;
using System.IO;
using Banda.Core;
using Banda.Heist;
using Banda.Net;
using NUnit.Framework;

namespace Banda.Tests
{
    /// <summary>
    /// Мережеве ядро без сокетів: хост і клієнти в одному процесі через MemoryNet.
    /// Перевіряє протокол, лобі, адресність знімків, обрив/повернення, відмови й стійкість до сміття.
    /// Те, що йде реальним транспортом (NGO), перевіряють боти: tools/net_test.sh.
    /// </summary>
    public sealed class NetCoreTests
    {
        private const float Frame = 1f / 15f;

        private sealed class Probe
        {
            public RoleStatic Static;
            public RoleSnapshot Snapshot;
            public HeistSummary Summary;
            public readonly List<string> Notices = new List<string>();

            public Probe(RoomClient client)
            {
                client.Transport.StaticReceived += s => Static = s;
                client.Transport.SnapshotReceived += s => Snapshot = s;
                client.Transport.Finished += s => Summary = s;
                client.Notice += n => Notices.Add(n);
            }
        }

        private MemoryNet _net;
        private RoomHost _host;
        private RoomClient _hostClient;
        private Probe _hostProbe;

        [SetUp]
        public void SetUp()
        {
            _net = new MemoryNet();
            _host = new RoomHost(_net.Server, "4242", HeistLevel.CornerStore, () => 7);
            _hostClient = new RoomClient(_host.CreateLocalLink(), "tok-host", "Хост");
            _hostProbe = new Probe(_hostClient);
            Settle();
        }

        private void Settle()
        {
            for (int i = 0; i < 30; i++)
            {
                _net.Pump();
                _host.Update(0f);
            }
        }

        private void Step(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                _host.Update(Frame);
                Settle();
            }
        }

        private RoomClient Join(string token, string name, out Probe probe)
        {
            RoomClient client = new RoomClient(_net.Connect(), token, name);
            probe = new Probe(client);
            Settle();
            return client;
        }

        private void StartWithThreeRoles(out RoomClient shadow, out RoomClient safe, out Probe shadowProbe, out Probe safeProbe)
        {
            shadow = Join("tok-shadow", "Тінь", out shadowProbe);
            safe = Join("tok-safe", "Сейф", out safeProbe);
            _hostClient.PickRole(RoleId.Hacker);
            shadow.PickRole(RoleId.Shadow);
            safe.PickRole(RoleId.Safecracker);
            Settle();
            _hostClient.RequestStart();
            Settle();
        }

        // ------------------------------------------------------------------ кодек

        [Test]
        public void Codec_Roundtrip_StaticAndSnapshot_ForEveryRole()
        {
            HeistSession session = new HeistSession(HeistLevel.CornerStore(), 7);
            session.AddPlayer("h", "Хакер", RoleId.Hacker);
            session.AddPlayer("s", "Тінь", RoleId.Shadow);
            session.AddPlayer("c", "Сейф", RoleId.Safecracker);
            session.Tick(0.5f);
            foreach (string token in new[] { "h", "s", "c" })
            {
                RoleStatic stat = session.StaticFor(token);
                byte[] encoded = WireCodec.Encode(MsgType.Static, w => WireCodec.WriteStatic(w, stat));
                WireCodec.ReadType(encoded, out BinaryReader r);
                RoleStatic back = WireCodec.ReadStatic(r);
                Assert.That(back.GetType(), Is.EqualTo(stat.GetType()));
                Assert.That(back.Role, Is.EqualTo(stat.Role));

                RoleSnapshot snap = session.SnapshotFor(token);
                byte[] snapBytes = WireCodec.Encode(MsgType.Snapshot, w => WireCodec.WriteSnapshot(w, snap));
                WireCodec.ReadType(snapBytes, out BinaryReader r2);
                RoleSnapshot snapBack = WireCodec.ReadSnapshot(r2);
                Assert.That(snapBack.View.GetType(), Is.EqualTo(snap.View.GetType()));
                Assert.That(snapBack.Shared.Players.Count, Is.EqualTo(3));
                Assert.That(snapBack.Shared.TimeLeft, Is.EqualTo(snap.Shared.TimeLeft));
            }

            HackerStatic hackerStatic = (HackerStatic)session.StaticFor("h");
            WireCodec.ReadType(WireCodec.Encode(MsgType.Static, w => WireCodec.WriteStatic(w, hackerStatic)), out BinaryReader rh);
            HackerStatic hackerBack = (HackerStatic)WireCodec.ReadStatic(rh);
            Assert.That(hackerBack.Walls, Is.EqualTo(hackerStatic.Walls));
            Assert.That(hackerBack.Cameras, Is.EqualTo(hackerStatic.Cameras));
            Assert.That(hackerBack.Safe, Is.EqualTo(hackerStatic.Safe));

            SafecrackerStatic comboStatic = (SafecrackerStatic)session.StaticFor("c");
            WireCodec.ReadType(WireCodec.Encode(MsgType.Static, w => WireCodec.WriteStatic(w, comboStatic)), out BinaryReader rc);
            Assert.That(((SafecrackerStatic)WireCodec.ReadStatic(rc)).Combo, Is.EqualTo(comboStatic.Combo));
        }

        [Test]
        public void Codec_Action_Roundtrip_AndDirectionIsClamped()
        {
            HeistAction forged = HeistAction.Move(new Vec2(50f, -50f));
            WireCodec.ReadType(WireCodec.Encode(MsgType.Action, w => WireCodec.WriteAction(w, forged)), out BinaryReader r);
            HeistAction back = WireCodec.ReadAction(r);
            Assert.That(back.Direction.Length, Is.LessThanOrEqualTo(1.0001f), "хост не вірить клієнту: швидкість Тіні обмежена");

            HeistAction safeTry = HeistAction.SafeTry(1, 2, 3);
            WireCodec.ReadType(WireCodec.Encode(MsgType.Action, w => WireCodec.WriteAction(w, safeTry)), out BinaryReader r2);
            Assert.That(WireCodec.ReadAction(r2).Numbers, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Codec_RejectsGarbage_WithProtocolException()
        {
            Assert.Throws<ProtocolException>(() => WireCodec.ReadType(new byte[0], out _));
            Assert.Throws<ProtocolException>(() => WireCodec.ReadType(new byte[] { 200 }, out _));
            // Лобі з нібито мільярдом гравців
            byte[] evil = WireCodec.Encode(MsgType.Lobby, w =>
            {
                WireCodec.WriteString(w, "1");
                w.Write((byte)0);
                w.Write(0);
                w.Write(false);
                w.Write(int.MaxValue);
            });
            WireCodec.ReadType(evil, out BinaryReader r);
            Assert.Throws<ProtocolException>(() => WireCodec.ReadLobby(r));
        }

        [Test]
        public void Host_SurvivesRandomGarbage_FromConnectedClient()
        {
            RoomClient client = Join("tok-a", "А", out _);
            System.Random rng = new System.Random(5);
            MemoryClientLink raw = _net.Connect();
            Settle();
            for (int i = 0; i < 200; i++)
            {
                byte[] junk = new byte[rng.Next(0, 40)];
                rng.NextBytes(junk);
                raw.Send(junk);
            }

            Assert.DoesNotThrow(Settle);
            Assert.That(client.State, Is.EqualTo(ClientState.Joined), "інші гравці нічого не помітили");
        }

        // ------------------------------------------------------------------ лобі

        [Test]
        public void Lobby_HostAndClients_SeeEachOther_AndRolesAreUnique()
        {
            RoomClient a = Join("tok-a", "Аня", out Probe pa);
            RoomClient b = Join("tok-b", "Богдан", out Probe pb);
            Assert.That(_hostClient.Lobby.Players.Count, Is.EqualTo(3));
            Assert.That(_hostClient.IsHost, Is.True);
            Assert.That(a.IsHost, Is.False);
            Assert.That(a.Welcome.RoomCode, Is.EqualTo("4242"));

            a.PickRole(RoleId.Shadow);
            Settle();
            b.PickRole(RoleId.Shadow);
            Settle();
            Assert.That(b.Role, Is.EqualTo(RoleId.None), "другий гравець не може взяти зайняту роль");
            Assert.That(pb.Notices, Has.Some.Contains("уже взяв"));
            Assert.That(a.Role, Is.EqualTo(RoleId.Shadow));
            Assert.That(_hostClient.Lobby.Players[1].Role, Is.EqualTo(RoleId.Shadow), "хост бачить роль Ані");

            a.PickRole(RoleId.None);
            Settle();
            b.PickRole(RoleId.Shadow);
            Settle();
            Assert.That(b.Role, Is.EqualTo(RoleId.Shadow), "звільнена роль береться");
        }

        [Test]
        public void Lobby_OnlyHostCanStart_AndOnlyWhenEveryoneHasRole()
        {
            RoomClient a = Join("tok-a", "Аня", out Probe pa);
            _hostClient.PickRole(RoleId.Hacker);
            Settle();
            a.RequestStart();
            Settle();
            Assert.That(pa.Notices, Has.Some.Contains("тільки хост"));
            Assert.That(_host.Phase, Is.EqualTo(RoomPhase.Lobby));

            _hostClient.RequestStart();
            Settle();
            Assert.That(_host.Phase, Is.EqualTo(RoomPhase.Lobby), "Аня ще без ролі");
            Assert.That(_hostProbe.Notices, Has.Some.Contains("кожен обрав роль"));
            Assert.That(_hostClient.Lobby.CanStart, Is.False);

            a.PickRole(RoleId.Shadow);
            Settle();
            Assert.That(_hostClient.Lobby.CanStart, Is.True);
            Assert.That(a.Lobby.CanStart, Is.False, "кнопка «Почати» є лише в хоста");
            _hostClient.RequestStart();
            Settle();
            Assert.That(_host.Phase, Is.EqualTo(RoomPhase.Heist));
        }

        [Test]
        public void Lobby_FullRoom_RejectsSixthPlayer_WithReason()
        {
            for (int i = 0; i < RoomHost.MaxPlayers - 1; i++)
            {
                Join("tok-" + i, "Г" + i, out _);
            }

            RoomClient extra = Join("tok-extra", "Зайвий", out _);
            Assert.That(extra.State, Is.EqualTo(ClientState.Rejected));
            Assert.That(extra.StateMessage, Does.Contain("повна"));
        }

        [Test]
        public void Lobby_WrongProtocolVersion_IsRejectedWithClearMessage()
        {
            MemoryClientLink link = _net.Connect();
            Settle();
            link.Send(WireCodec.Encode(MsgType.Hello, w =>
            {
                w.Write((byte)99);
                WireCodec.WriteString(w, "tok-old");
                WireCodec.WriteString(w, "Старий");
            }));
            WelcomeInfo welcome = null;
            link.MessageReceived += d =>
            {
                if (WireCodec.ReadType(d, out BinaryReader r) == MsgType.Welcome)
                {
                    welcome = WireCodec.ReadWelcome(r);
                }
            };
            Settle();
            Assert.That(welcome, Is.Not.Null);
            Assert.That(welcome.Ok, Is.False);
            Assert.That(welcome.Error, Does.Contain("Версія"));
        }

        [Test]
        public void Lobby_ConnectionWithoutHello_IsDroppedAfterTimeout()
        {
            MemoryClientLink silent = _net.Connect();
            bool dropped = false;
            silent.Disconnected += () => dropped = true;
            Settle();
            _host.Update(RoomHost.HelloTimeoutSeconds + 1f);
            Settle();
            Assert.That(dropped, Is.True);
        }

        [Test]
        public void Lobby_VanishedPlayer_LosesSeatAfterGrace_ButCanReturnBefore()
        {
            RoomClient a = Join("tok-a", "Аня", out _);
            a.PickRole(RoleId.Shadow);
            Settle();
            _net.Cut((MemoryClientLink)GetLink(a));
            Settle();
            Assert.That(_hostClient.Lobby.Players[1].Connected, Is.False);
            Assert.That(_hostClient.Lobby.Players[1].Role, Is.EqualTo(RoleId.Shadow), "роль поки за нею");

            RoomClient back = new RoomClient(_net.Connect(), "tok-a", "Аня");
            Settle();
            Assert.That(back.Welcome.Resumed, Is.True);
            Assert.That(back.Role, Is.EqualTo(RoleId.Shadow));

            _net.Cut((MemoryClientLink)GetLink(back));
            Settle();
            _host.Update(RoomHost.LobbyGraceSeconds + 1f);
            Settle();
            Assert.That(_hostClient.Lobby.Players.Count, Is.EqualTo(1), "місце звільнилось");
        }

        // ------------------------------------------------------------------ справа

        [Test]
        public void Heist_EachClientGetsOnlyItsOwnRoleData()
        {
            StartWithThreeRoles(out RoomClient shadow, out RoomClient safe, out Probe shadowProbe, out Probe safeProbe);
            Step(5);

            Assert.That(_hostProbe.Static, Is.TypeOf<HackerStatic>());
            Assert.That(_hostProbe.Snapshot.View, Is.TypeOf<HackerView>());
            Assert.That(shadowProbe.Static, Is.TypeOf<ShadowStatic>());
            Assert.That(shadowProbe.Snapshot.View, Is.TypeOf<ShadowView>());
            Assert.That(safeProbe.Static, Is.TypeOf<SafecrackerStatic>());
            Assert.That(safeProbe.Snapshot.View, Is.TypeOf<SafecrackerView>());
            Assert.That(((SafecrackerStatic)safeProbe.Static).Combo.Length, Is.EqualTo(HeistSim.ComboLength));

            // Жоден пакет, адресований Тіні, не містить типів інших ролей — перевіряємо те, що реально пішло в мережу.
            int shadowId = ClientId(shadow);
            int safeId = ClientId(safe);
            foreach (KeyValuePair<int, byte[]> sent in _net.SentToClients)
            {
                MsgType type = WireCodec.ReadType(sent.Value, out BinaryReader r);
                if (type == MsgType.Static)
                {
                    RoleStatic st = WireCodec.ReadStatic(r);
                    RoleId expected = sent.Key == shadowId ? RoleId.Shadow : sent.Key == safeId ? RoleId.Safecracker : RoleId.None;
                    Assert.That(st.Role, Is.EqualTo(expected), "статика пішла не тій ролі");
                }
                else if (type == MsgType.Snapshot)
                {
                    RoleSnapshot snap = WireCodec.ReadSnapshot(r);
                    RoleId expected = sent.Key == shadowId ? RoleId.Shadow : sent.Key == safeId ? RoleId.Safecracker : RoleId.None;
                    Assert.That(snap.View.Role, Is.EqualTo(expected), "знімок пішов не тій ролі");
                }
            }
        }

        [Test]
        public void Heist_ActionsWork_AndForgedRoleActionsDoNothing()
        {
            StartWithThreeRoles(out RoomClient shadow, out RoomClient safe, out Probe shadowProbe, out Probe safeProbe);
            Step(3);
            Vec2 before = ((ShadowView)shadowProbe.Snapshot.View).Position;
            shadow.Transport.Send(HeistAction.Move(new Vec2(1f, 0f)));
            Step(15);
            Vec2 after = ((ShadowView)shadowProbe.Snapshot.View).Position;
            Assert.That(after.X, Is.GreaterThan(before.X + 5f), "Тінь рухається за дією клієнта");

            // Хакер шле дію Тіні — хост бере роль із реєстру, тож Тінь не рухається більше.
            Vec2 shadowNow = _host.Session.Sim.ShadowPos;
            _hostClient.Transport.Send(HeistAction.Move(new Vec2(0f, 1f)));
            Step(3);
            // (Тінь продовжує попередній рух по X; по Y зміни від чужої дії немає)
            Assert.That(Math.Abs(_host.Session.Sim.ShadowPos.Y - shadowNow.Y), Is.LessThan(0.01f));

            _hostClient.Transport.Send(HeistAction.CameraOff(0));
            Step(3);
            Assert.That(((HackerView)_hostProbe.Snapshot.View).CamerasOff[0], Is.GreaterThan(0f), "камера вимкнена дією Хакера");
        }

        [Test]
        public void Heist_WrongCombo_RaisesAlarmAndMistake_RightComboOpens()
        {
            StartWithThreeRoles(out _, out RoomClient safe, out _, out Probe safeProbe);
            Step(3);
            int[] combo = ((SafecrackerStatic)safeProbe.Static).Combo;
            int range = ((SafecrackerStatic)safeProbe.Static).Range;
            int[] wrong = new int[combo.Length];
            for (int i = 0; i < combo.Length; i++)
            {
                wrong[i] = (combo[i] + 1) % range;
            }

            safe.Transport.Send(HeistAction.SafeTry(wrong));
            Step(3);
            Assert.That(((SafecrackerView)safeProbe.Snapshot.View).Mistakes, Is.EqualTo(1));
            Assert.That(safeProbe.Snapshot.Shared.Alarm, Is.GreaterThanOrEqualTo(HeistSim.AlarmSafeMistake - 0.01f));
            Assert.That(safeProbe.Snapshot.Shared.Players.Count, Is.EqualTo(3), "у спільному стані троє гравців");

            safe.Transport.Send(HeistAction.SafeTry(combo));
            Step(3);
            Assert.That(((SafecrackerView)safeProbe.Snapshot.View).Open, Is.True);
        }

        [Test]
        public void Heist_Reconnect_ReturnsToSameRole_WithSavedState()
        {
            StartWithThreeRoles(out _, out RoomClient safe, out _, out Probe safeProbe);
            Step(3);
            int[] combo = ((SafecrackerStatic)safeProbe.Static).Combo;
            int range = ((SafecrackerStatic)safeProbe.Static).Range;
            int[] wrong = new int[combo.Length];
            for (int i = 0; i < combo.Length; i++)
            {
                wrong[i] = (combo[i] + 1) % range;
            }

            safe.Transport.Send(HeistAction.SafeTry(wrong));
            Step(3);

            _net.Cut((MemoryClientLink)GetLink(safe));
            Settle();
            Step(5);
            Assert.That(_host.Session.IsConnected("tok-safe"), Is.False);
            Assert.That(_hostClient.Lobby.Players[2].Connected, Is.False, "решта банди бачить обрив");

            RoomClient back = new RoomClient(_net.Connect(), "tok-safe", "Сейф");
            Probe backProbe = new Probe(back);
            Settle();
            Step(3);
            Assert.That(back.State, Is.EqualTo(ClientState.Joined));
            Assert.That(back.Welcome.Resumed, Is.True);
            Assert.That(back.Role, Is.EqualTo(RoleId.Safecracker));
            Assert.That(backProbe.Static, Is.TypeOf<SafecrackerStatic>(), "статику ролі прислано знову");
            Assert.That(((SafecrackerStatic)backProbe.Static).Combo, Is.EqualTo(combo));
            Assert.That(((SafecrackerView)backProbe.Snapshot.View).Mistakes, Is.EqualTo(1), "стан сейфа збережено");

            back.Transport.Send(HeistAction.SafeTry(combo));
            Step(3);
            Assert.That(((SafecrackerView)backProbe.Snapshot.View).Open, Is.True);
        }

        [Test]
        public void Heist_ShadowDisconnect_StopsShadow()
        {
            StartWithThreeRoles(out RoomClient shadow, out _, out _, out _);
            Step(3);
            shadow.Transport.Send(HeistAction.Move(new Vec2(1f, 0f)));
            Step(10);
            _net.Cut((MemoryClientLink)GetLink(shadow));
            Settle();
            Vec2 stopped = _host.Session.Sim.ShadowPos;
            Step(30);
            Assert.That(_host.Session.Sim.ShadowPos.DistanceTo(stopped), Is.LessThan(0.5f), "Тінь без «пальця» не біжить далі сама");
        }

        [Test]
        public void Heist_NewPlayer_CannotJoinRunningHeist()
        {
            StartWithThreeRoles(out _, out _, out _, out _);
            RoomClient late = Join("tok-late", "Запізнілий", out _);
            Assert.That(late.State, Is.EqualTo(ClientState.Rejected));
            Assert.That(late.StateMessage, Does.Contain("Справа вже почалась"));
        }

        [Test]
        public void Heist_FinishedSummary_ReachesEveryone_AndHostCanReturnToLobby()
        {
            StartWithThreeRoles(out RoomClient shadow, out _, out Probe shadowProbe, out Probe safeProbe);
            Step(2);
            // Час справи спливає: доганяємо таймер великим кроком
            for (int i = 0; i < 700 && _host.Phase == RoomPhase.Heist; i++)
            {
                _host.Update(1f);
                Settle();
            }

            Assert.That(_host.Phase, Is.EqualTo(RoomPhase.Finished));
            Assert.That(_hostProbe.Summary, Is.Not.Null);
            Assert.That(shadowProbe.Summary.Outcome, Is.Not.EqualTo(HeistOutcome.None), "справа завершилась (час чи тривога)");
            Assert.That(safeProbe.Summary, Is.Not.Null);

            shadow.RequestBackToLobby();
            Settle();
            Assert.That(_host.Phase, Is.EqualTo(RoomPhase.Finished), "лише хост повертає банду в лобі");
            _hostClient.RequestBackToLobby();
            Settle();
            Assert.That(_host.Phase, Is.EqualTo(RoomPhase.Lobby));
            Assert.That(shadow.Lobby.Phase, Is.EqualTo(RoomPhase.Lobby));
        }

        [Test]
        public void Host_Close_TellsEveryoneWhy()
        {
            RoomClient a = Join("tok-a", "Аня", out _);
            _host.Close("Хост завершив гру.");
            Settle();
            Assert.That(a.State, Is.EqualTo(ClientState.Closed));
            Assert.That(a.StateMessage, Is.EqualTo("Хост завершив гру."));
            Assert.That(_hostClient.State, Is.EqualTo(ClientState.Closed));
        }

        [Test]
        public void Client_LostConnection_BecomesLost_NotClosed()
        {
            RoomClient a = Join("tok-a", "Аня", out _);
            _net.Cut((MemoryClientLink)GetLink(a));
            Settle();
            Assert.That(a.State, Is.EqualTo(ClientState.Lost));
            Assert.That(a.StateMessage, Is.Not.Empty);
        }

        // ------------------------------------------------------------------ допоміжне

        private static readonly System.Reflection.FieldInfo LinkField = typeof(RoomClient).GetField("_link", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private static IClientLink GetLink(RoomClient client) => (IClientLink)LinkField.GetValue(client);

        private static int ClientId(RoomClient client) => ((MemoryClientLink)GetLink(client)).Id;
    }
}
