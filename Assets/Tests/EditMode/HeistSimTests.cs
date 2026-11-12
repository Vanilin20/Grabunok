using System.Linq;
using System.Reflection;
using Banda.Core;
using Banda.Heist;
using NUnit.Framework;

namespace Banda.Tests
{
    /// <summary>
    /// Тести симуляції справи. Перевірки ролей відтворюють мережеві тести Godot (tests/net_bot.gd),
    /// але без мережі: фільтрацію гарантує HeistSim/HeistSession, мережа лише перенесе знімки.
    /// </summary>
    public sealed class HeistSimTests
    {
        private const float Dt = 1f / 60f;

        private static HeistSim NewSim(HeistLevel level = null, int seed = 7) => new HeistSim(level ?? HeistLevel.CornerStore(), seed);

        private static void Run(HeistSim sim, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                sim.Tick(Dt);
            }
        }

        /// <summary>Перетягнути Тінь у точку (рухом, без телепорту) — для тестів детекції.</summary>
        private static void WalkTo(HeistSim sim, Vec2 target, float maxSeconds = 20f)
        {
            for (float t = 0f; t < maxSeconds && sim.ShadowPos.DistanceTo(target) > 1.5f; t += Dt)
            {
                Vec2 dir = (target - sim.ShadowPos).LimitLength(1f);
                Vec2 unit = dir.Length > 0f ? dir * (1f / dir.Length) : Vec2.Zero;
                sim.Apply(RoleId.Shadow, HeistAction.Move(unit));
                sim.Tick(Dt);
            }

            sim.Apply(RoleId.Shadow, HeistAction.Move(Vec2.Zero));
        }

        private static string[] Members(System.Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();

        // --- Фільтрація по ролях ---------------------------------------------------------------

        [Test]
        public void Hacker_GetsSchemeAndCameras_ButNoComboAndNoShadow()
        {
            HeistSim sim = NewSim();
            HackerStatic st = (HackerStatic)sim.StaticViewFor(RoleId.Hacker);
            Assert.That(st.Cameras.Count, Is.EqualTo(sim.Level.Cameras.Count), "Хакер має схему з камерами");
            Assert.That(Members(typeof(HackerStatic)), Has.None.Contains("Combo"), "у статиці Хакера немає комбінації");
            string[] view = Members(typeof(HackerView));
            Assert.That(view, Does.Contain("CamerasOff"), "Хакер бачить стан камер");
            Assert.That(view, Has.None.Contains("Position").And.None.Contains("Shadow").And.None.Contains("Mistakes"),
                "Хакер не отримує позицію Тіні й стан сейфа");
        }

        [Test]
        public void Shadow_GetsOwnCircle_ButNoSchemeNoCameraStateNoCombo()
        {
            HeistSim sim = NewSim();
            Assert.That(Members(typeof(ShadowStatic)), Is.EquivalentTo(new[] { "ViewRadius" }), "Тінь не отримує ні схему, ні комбінацію");
            ShadowView view = (ShadowView)sim.ViewFor(RoleId.Shadow);
            Assert.That(view.Position, Is.EqualTo(sim.ShadowPos), "Тінь отримує свою позицію");
            Assert.That(Members(typeof(ShadowView)), Has.None.Contains("CamerasOff").And.None.Contains("Combo"),
                "Тінь не отримує стан камер і комбінацію");
        }

        [Test]
        public void Shadow_SeesOnlyWhatIsInsideViewRadius()
        {
            HeistSim sim = NewSim();
            ShadowView view = (ShadowView)sim.ViewFor(RoleId.Shadow);
            float r = HeistSim.ShadowViewRadius;
            Assert.That(view.GuardVisible, Is.EqualTo(sim.ShadowPos.DistanceTo(sim.GuardPos) <= r));
            Assert.That(view.SafeVisible, Is.False, "сейф у підсобці далеко від входу — не видно");
            foreach (Vec2 cam in view.Cameras)
            {
                Assert.That(sim.ShadowPos.DistanceTo(cam), Is.LessThanOrEqualTo(r));
            }

            Box area = Box.Around(sim.ShadowPos, r);
            foreach (Box wall in view.Walls)
            {
                Assert.That(wall.X >= area.X - 0.01f && wall.Right <= area.Right + 0.01f, "стіни обрізані колом видимості");
            }
        }

        [Test]
        public void Safecracker_GetsComboAndSafeState_ButNoScheme()
        {
            HeistSim sim = NewSim();
            SafecrackerStatic st = (SafecrackerStatic)sim.StaticViewFor(RoleId.Safecracker);
            Assert.That(st.Combo, Is.EqualTo(sim.Combo), "Зломщик має комбінацію");
            Assert.That(Members(typeof(SafecrackerStatic)), Has.None.Contains("Cameras").And.None.Contains("Walls"), "і не має схеми");
            Assert.That(Members(typeof(SafecrackerView)), Is.EquivalentTo(new[] { "Open", "Mistakes" }));
        }

        [Test]
        public void Session_SnapshotContainsOnlyOwnRoleView()
        {
            HeistSession session = NewSession();
            Assert.That(session.SnapshotFor("t-hacker").View, Is.InstanceOf<HackerView>());
            Assert.That(session.SnapshotFor("t-shadow").View, Is.InstanceOf<ShadowView>());
            Assert.That(session.SnapshotFor("t-safe").View, Is.InstanceOf<SafecrackerView>());
            Assert.That(session.StaticFor("t-shadow"), Is.InstanceOf<ShadowStatic>());
            Assert.That(session.SnapshotFor("t-safe").Shared.Players.Count, Is.EqualTo(3), "у спільному стані 3 гравці");
        }

        [Test]
        public void Session_RejectsActionFromWrongRole()
        {
            HeistSession session = NewSession();
            Assert.That(session.Submit("t-shadow", HeistAction.CameraOff(0)), Is.False, "Тінь не може вимкнути камеру");
            Assert.That(session.Sim.CameraOffLeft(0), Is.EqualTo(0f));
            Assert.That(session.Submit("t-hacker", HeistAction.CameraOff(0)), Is.True);
            Assert.That(session.Sim.CameraOffLeft(0), Is.GreaterThan(0f), "камера 0 вимкнулась після дії Хакера");
            Assert.That(session.Submit("невідомий", HeistAction.DoorToggle()), Is.False);
        }

        [Test]
        public void Session_ReconnectMidHeist_ReturnsSameRoleAndState()
        {
            HeistSession session = NewSession();
            session.Submit("t-safe", HeistAction.SafeTry(99, 99, 99));
            session.Tick(0.5f);
            session.MarkDisconnected("t-safe");
            Assert.That(session.Submit("t-safe", HeistAction.SafeTry(session.Sim.Combo)), Is.False, "без зв'язку дії не приймаються");
            session.Tick(1f);
            Assert.That(session.Reconnect("t-safe"), Is.EqualTo(RoleId.Safecracker), "повернувся на роль Зломщика посеред справи");
            SafecrackerView view = (SafecrackerView)session.SnapshotFor("t-safe").View;
            Assert.That(view.Mistakes, Is.EqualTo(1), "стан сейфа збережено після перепідключення");
            Assert.That(session.Submit("t-safe", HeistAction.SafeTry(((SafecrackerStatic)session.StaticFor("t-safe")).Combo)), Is.True);
            Assert.That(((SafecrackerView)session.SnapshotFor("t-safe").View).Open, Is.True, "правильна комбінація відкрила сейф");
        }

        // --- Тривога й пороги -------------------------------------------------------------------

        [Test]
        public void Camera_SeesShadow_AlarmGrows30PerSecond()
        {
            HeistSim sim = NewSim();
            sim.Apply(RoleId.Hacker, HeistAction.DoorToggle());
            Vec2 cam = sim.Level.Cameras[0];
            WalkTo(sim, cam + new Vec2(0f, 20f));
            float before = sim.Alarm;
            Assert.That(sim.ShadowSeen, Is.True, "камера бачить Тінь");
            sim.Tick(1f);
            Assert.That(sim.Alarm - before, Is.EqualTo(HeistSim.AlarmCameraPerSec).Within(0.5f));
        }

        [Test]
        public void Camera_Off_DoesNotSee()
        {
            HeistSim sim = NewSim();
            Vec2 cam = sim.Level.Cameras[0];
            sim.Apply(RoleId.Hacker, HeistAction.CameraOff(0));
            WalkTo(sim, cam + new Vec2(0f, 20f), 3f);
            float before = sim.Alarm;
            sim.Tick(0.5f);
            if (sim.ShadowPos.DistanceTo(sim.GuardPos) > HeistSim.GuardRange && !sim.InFlashlight(sim.ShadowPos))
            {
                Assert.That(sim.Alarm, Is.EqualTo(before), "вимкнена камера не піднімає тривогу");
            }
        }

        [Test]
        public void Alarm_AtFifty_GuardSpeedsUp_AtHundred_Caught()
        {
            HeistSim sim = NewSim(seed: 3);
            for (int i = 0; i < 5; i++)
            {
                sim.Apply(RoleId.Safecracker, HeistAction.SafeTry(-1, -1, -1));
            }

            Assert.That(sim.Alarm, Is.EqualTo(50f).Within(0.01f));
            Assert.That(sim.Alert, Is.True, "на 50% охорона посилюється");
            Vec2 a = sim.GuardPos;
            sim.Tick(1f);
            Assert.That(a.DistanceTo(sim.GuardPos), Is.EqualTo(HeistSim.GuardSpeed * HeistSim.GuardAlertSpeedMult).Within(0.6f),
                "охоронець ходить у 1.5 раза швидше");
            for (int i = 0; i < 5; i++)
            {
                sim.Apply(RoleId.Safecracker, HeistAction.SafeTry(-1, -1, -1));
            }

            sim.Tick(Dt);
            Assert.That(sim.Outcome, Is.EqualTo(HeistOutcome.Caught), "100% — провал");
            Assert.That(sim.Summary().MaxAlarm, Is.EqualTo(100));
        }

        [Test]
        public void Hidden_NearCover_IsNotSeen()
        {
            HeistSim sim = NewSim();
            Box shelf = sim.Level.Covers[1]; // стелаж Б — найближчий до входу, прямий шлях без перешкод
            WalkTo(sim, new Vec2(shelf.X + 10f, shelf.Bottom + 6f));
            Assert.That(sim.NearCover(sim.ShadowPos), Is.True);
            sim.Apply(RoleId.Shadow, HeistAction.Hide());
            Assert.That(sim.ShadowHidden, Is.True);
            sim.Tick(Dt);
            Assert.That(sim.ShadowSeen, Is.False, "схована Тінь невидима");
            sim.Apply(RoleId.Shadow, HeistAction.Move(new Vec2(1f, 0f)));
            sim.Tick(Dt);
            Assert.That(sim.ShadowHidden, Is.False, "рух виводить зі схованки");
        }

        [Test]
        public void Flashlight_SeesAlongFacing_ButNotThroughShelf()
        {
            HeistSim sim = NewSim();
            sim.Tick(0.2f); // охоронець рушив — є напрям погляду
            float f = sim.GuardFacing;
            Vec2 ahead = sim.GuardPos + new Vec2(System.MathF.Cos(f), System.MathF.Sin(f)) * 40f;
            Vec2 behind = sim.GuardPos - new Vec2(System.MathF.Cos(f), System.MathF.Sin(f)) * 40f;
            Assert.That(sim.InFlashlight(ahead), Is.True, "ліхтарик світить уперед");
            Assert.That(sim.InFlashlight(behind), Is.False, "за спиною охоронець не бачить");
        }

        // --- Сейф, успіх, таймер ---------------------------------------------------------------

        [Test]
        public void Safe_WrongCombo_IsMistakeAndAlarm_RightCombo_Opens()
        {
            HeistSim sim = NewSim();
            sim.Apply(RoleId.Safecracker, HeistAction.SafeTry(99, 99, 99));
            Assert.That(sim.SafeMistakes, Is.EqualTo(1), "хибна комбінація зарахована як помилка");
            Assert.That(sim.Alarm, Is.EqualTo(HeistSim.AlarmSafeMistake).Within(0.01f), "помилка на сейфі підняла тривогу");
            Assert.That(sim.SafeOpen, Is.False);
            sim.Apply(RoleId.Safecracker, HeistAction.SafeTry(sim.Combo));
            Assert.That(sim.SafeOpen, Is.True, "правильна комбінація відкрила сейф");
            Assert.That(sim.Apply(RoleId.Safecracker, HeistAction.SafeTry(99, 99, 99)), Is.False, "відчинений сейф більше не крутять");
        }

        [Test]
        public void Combo_IsDeterministicPerSeed_AndInRange()
        {
            int[] a = NewSim(seed: 42).Combo;
            int[] b = NewSim(seed: 42).Combo;
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.All(n => n >= 0 && n < HeistSim.ComboRange), Is.True);
        }

        [Test]
        public void FullHeist_TakeLootAndExit_IsSuccess()
        {
            HeistSim sim = NewSim();
            sim.Apply(RoleId.Safecracker, HeistAction.SafeTry(sim.Combo));
            sim.Apply(RoleId.Hacker, HeistAction.DoorToggle());
            // Шлях обходить камеру знизу й іде до сейфа через двері підсобки.
            Vec2[] route =
            {
                new Vec2(4.5f * 16, 11 * 16), new Vec2(14.5f * 16, 11 * 16), new Vec2(14.5f * 16, 6 * 16),
                new Vec2(17.5f * 16, 6 * 16), new Vec2(17.5f * 16, 3.5f * 16), new Vec2(19 * 16 + 8, 3.5f * 16),
            };
            sim.Apply(RoleId.Hacker, HeistAction.CameraOff(0));
            foreach (Vec2 p in route)
            {
                WalkTo(sim, p);
            }

            Assert.That(sim.Apply(RoleId.Shadow, HeistAction.Take()), Is.True, "Тінь бере здобич біля відчиненого сейфа");
            sim.Apply(RoleId.Hacker, HeistAction.CameraOff(0));
            for (int i = route.Length - 1; i >= 0; i--)
            {
                WalkTo(sim, route[i]);
            }

            WalkTo(sim, sim.Level.ShadowStart);
            sim.Tick(Dt);
            Assert.That(sim.Outcome, Is.EqualTo(HeistOutcome.Success),
                $"Тінь зі здобиччю на виході — успіх (тривога {sim.Alarm:0}, позиція {sim.ShadowPos})");
            Assert.That(sim.Summary().Loot, Is.True);
        }

        [Test]
        public void Timer_RunsOut_IsTimeout()
        {
            HeistSim sim = NewSim();
            sim.Tick(HeistSim.Duration - 1f);
            Assert.That(sim.IsFinished, Is.False);
            Assert.That(sim.TimeLeft, Is.EqualTo(1f).Within(0.01f));
            sim.Tick(1.01f);
            Assert.That(sim.Outcome, Is.EqualTo(HeistOutcome.Timeout), "прийшла ранкова зміна");
            Assert.That(sim.Summary().TimeUsedSeconds, Is.EqualTo((int)HeistSim.Duration));
        }

        [Test]
        public void Door_DoesNotCloseOnShadow_AndBlocksWhenClosed()
        {
            HeistSim sim = NewSim();
            Box door = sim.Level.Door;
            Assert.That(sim.Apply(RoleId.Hacker, HeistAction.CameraOff(0)), Is.True);
            WalkTo(sim, new Vec2(door.X - 6f, door.Center.Y));
            WalkTo(sim, new Vec2(door.Right + 8f, door.Center.Y), 2f);
            Assert.That(sim.ShadowPos.X, Is.LessThan(door.X), "зачинені двері не пропускають");
            sim.Apply(RoleId.Hacker, HeistAction.DoorToggle());
            WalkTo(sim, door.Center, 2f);
            Assert.That(sim.Apply(RoleId.Hacker, HeistAction.DoorToggle()), Is.False, "двері не зачиняються на Тіні");
            Assert.That(sim.DoorOpen, Is.True);
        }

        [Test]
        public void GodotTestFloor_ShadowMovesRight()
        {
            HeistSim sim = NewSim(HeistLevel.GodotTestFloor());
            float x0 = sim.ShadowPos.X;
            sim.Apply(RoleId.Shadow, HeistAction.Move(new Vec2(1f, 0f)));
            Run(sim, 1f);
            Assert.That(sim.ShadowPos.X, Is.GreaterThan(x0 + 20f), "Тінь рухається");
        }

        [Test]
        public void Status_BusyAfterAction_DangerWhenSeen()
        {
            HeistSim sim = NewSim();
            Assert.That(sim.StatusFor(RoleId.Hacker), Is.EqualTo(PlayerStatus.Waiting));
            sim.Apply(RoleId.Hacker, HeistAction.DoorToggle());
            Assert.That(sim.StatusFor(RoleId.Hacker), Is.EqualTo(PlayerStatus.Busy));
            sim.Tick(HeistSim.BusyWindow + 0.1f);
            Assert.That(sim.StatusFor(RoleId.Hacker), Is.EqualTo(PlayerStatus.Waiting));
        }

        // --- «Гаряче місце» --------------------------------------------------------------------

        [Test]
        public void LocalTransport_HotSeat_SwitchesRoleAndFiltersData()
        {
            HeistSession session = NewSession();
            LocalTransport transport = new LocalTransport(session, RoleId.Shadow);
            RoleStatic lastStatic = null;
            RoleSnapshot lastSnap = null;
            transport.StaticReceived += s => lastStatic = s;
            transport.SnapshotReceived += s => lastSnap = s;
            transport.Begin();
            Assert.That(lastStatic, Is.InstanceOf<ShadowStatic>());
            transport.Send(HeistAction.CameraOff(0));
            Assert.That(session.Sim.CameraOffLeft(0), Is.EqualTo(0f), "з екрана Тіні камеру не вимкнути");
            Assert.That(transport.SwitchRole(RoleId.Hacker), Is.True);
            Assert.That(lastStatic, Is.InstanceOf<HackerStatic>());
            Assert.That(lastSnap.View, Is.InstanceOf<HackerView>());
            transport.Send(HeistAction.CameraOff(0));
            Assert.That(session.Sim.CameraOffLeft(0), Is.GreaterThan(0f));
            int snapshots = 0;
            transport.SnapshotReceived += _ => snapshots++;
            for (int i = 0; i < 60; i++)
            {
                transport.Update(Dt);
            }

            Assert.That(snapshots, Is.InRange(13, 16), "≈15 знімків на секунду");
        }

        private static HeistSession NewSession()
        {
            HeistSession session = new HeistSession(HeistLevel.CornerStore(), 11);
            session.AddPlayer("t-hacker", "Хост", RoleId.Hacker);
            session.AddPlayer("t-shadow", "Тінь", RoleId.Shadow);
            session.AddPlayer("t-safe", "Сейф", RoleId.Safecracker);
            return session;
        }
    }
}
