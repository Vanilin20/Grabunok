using System;
using System.Collections;
using System.IO;
using Banda.Core;
using Banda.Net;
using Banda.Heist;
using Banda.HeistRuntime;
using UnityEngine;

namespace Banda.UI
{
    /// <summary>
    /// Автоводій інтерфейсу для перевірки мережі на справжній збірці: запускається прапорцем «-uidrive host|client»,
    /// проходить екрани так, як це робить гравець (меню, вибір кімнати, лобі, справа, обрив, хост вийшов) і знімає скріншоти
    /// в теку «-shotdir». Без прапорця нічого не робить. Скрипт запуску — tools/net_ui_test.sh.
    /// </summary>
    public static class UiDrive
    {
        private static string _dir = "Captures/net_ui";
        private static string _role = string.Empty;
        private static readonly string Code = "4242";
        private static int _problems;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            _role = Arg(args, "-uidrive");
            if (_role == null)
            {
                return;
            }

            GameState.UseEphemeralToken("uidrive-" + _role);
            _dir = Arg(args, "-shotdir") ?? _dir;
            Directory.CreateDirectory(_dir);
            Application.runInBackground = true;
            GameObject go = new GameObject("UiDrive");
            UnityEngine.Object.DontDestroyOnLoad(go);
            UiDriveRunner runner = go.AddComponent<UiDriveRunner>();
            runner.StartCoroutine(Watchdog());
            runner.StartCoroutine(_role == "host" ? HostFlow() : ClientFlow());
        }

        private static string Arg(string[] args, string key)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == key)
                {
                    return i + 1 < args.Length ? args[i + 1] : string.Empty;
                }
            }

            return null;
        }

        private static void Log(string line)
        {
            Debug.Log("[uidrive:" + _role + "] " + line);
        }

        private static void Verify(string what, bool ok)
        {
            Log((ok ? "OK " : "ПРОБЛЕМА ") + what);
            if (!ok)
            {
                _problems++;
            }
        }

        private static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
            yield return new WaitForSecondsRealtime(0.4f);
        }

        private static IEnumerator Until(Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    Verify("очікування «" + what + "»", false);
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>Тест не має висіти вічно: якщо сценарій не закінчився за 3 хвилини (чи впав), вікно закривається з помилкою.</summary>
        private static IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(180f);
            Log("ПРОБЛЕМА сценарій не закінчився за 3 хвилини");
            Application.Quit(1);
        }

        private static IEnumerator Quit()
        {
            Log(_problems == 0 ? "РЕЗУЛЬТАТ: OK" : "РЕЗУЛЬТАТ: проблем " + _problems);
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(_problems == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ хост (Хакер)

        private static IEnumerator HostFlow()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            GameState state = GameState.Instance;
            state.SetMyName("Хакер");
            yield return Shot("h01_menu");
            NetDialog.Instance.ShowHostChoice();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("h02_choice");
            state.HostOnlineRoom(RoomKind.Lan, Code);
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Until(() => state.Phase == GamePhase.Lobby, 12f, "лобі хоста");
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("h03_lobby_alone");
            state.RequestPickRole(RoleId.Hacker);
            yield return Until(() => state.LobbyPlayers.Count >= 3 && state.CanStart(), 70f, "банда готова (3 гравці з ролями)");
            yield return new WaitForSecondsRealtime(0.5f);
            Verify("у лобі троє гравців із різними ролями", state.LobbyPlayers.Count == 3);
            yield return Shot("h04_lobby_full");
            state.StartHeist();
            yield return Until(() => state.Phase == GamePhase.Heist, 8f, "справа почалась");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("h05_heist_hacker");
            yield return HackDemo();
            // Клієнт-Тінь у цей час рве зв'язок і повертається; хост бачить обрив у списку банди.
            yield return new WaitForSecondsRealtime(6f);
            yield return Shot("h06_heist_later");
            yield return new WaitForSecondsRealtime(14f);
            state.LeaveRoom();
            yield return new WaitForSecondsRealtime(0.8f);
            Verify("після виходу хост у меню", state.Phase == GamePhase.Menu);
            yield return Shot("h07_menu_after");
            yield return Quit();
        }

        /// <summary>Хакер: головоломка з дротами від відкриття до результату (успіх і провал), зі скріншотами.</summary>
        private static IEnumerator HackDemo()
        {
            HackerScreen screen = UnityEngine.Object.FindFirstObjectByType<HackerScreen>(FindObjectsInactive.Include);
            ITransport transport = HeistDirector.Instance != null ? HeistDirector.Instance.Transport : null;
            Verify("екран Хакера й транспорт на місці", screen != null && transport != null && screen.Board != null);
            if (screen == null || transport == null || screen.Board == null)
            {
                yield break;
            }

            HackBoard board = screen.Board;
            RoleSnapshot last = null;
            transport.SnapshotReceived += s => last = s;
            yield return new WaitForSecondsRealtime(0.5f);

            screen.TapTarget(0);
            yield return Until(() => board.IsOpen && board.Mode == HackMode.Puzzle, 5f, "вікно взлому відкрилось після тапу по камері");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("h10_hack_open");
            int[] sol = HackPuzzle.Solution(board.Right);
            Verify("головоломка прийшла від хоста (4 дроти)", board.Wires == HeistSim.HackWiresCamera);
            board.DebugConnect(0, sol[0]);
            board.DebugConnect(1, sol[1]);
            board.DebugDragHalf(2, sol[2], 0.55f);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Shot("h11_hack_dragging");
            board.DebugDrop(sol[2]);
            board.DebugConnect(3, sol[3]);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("h12_hack_ready");
            board.DebugPressSubmit();
            yield return Until(() => board.Mode == HackMode.Solved, 5f, "хост прийняв розвʼязок");
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Shot("h13_hack_solved");
            yield return Until(() => !board.IsOpen, 4f, "вікно закрилось після успіху");
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("h14_camera_off");
            Verify("камера 1 вимкнена розвʼязаною головоломкою", last != null && last.View is HackerView hv && hv.CamerasOff.Count > 0 && hv.CamerasOff[0] > 0f);

            // Провал: дроти навхрест.
            screen.TapTarget(1);
            yield return Until(() => board.IsOpen && board.Mode == HackMode.Puzzle, 5f, "друге вікно взлому");
            int[] sol2 = HackPuzzle.Solution(board.Right);
            for (int w = 0; w < board.Wires; w++)
            {
                board.DebugConnect(w, sol2[(w + 1) % board.Wires]);
            }

            yield return new WaitForSecondsRealtime(0.2f);
            board.DebugPressSubmit();
            yield return Until(() => board.Mode == HackMode.Locked, 5f, "хост відхилив хибний розвʼязок");
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Shot("h15_hack_wrong");
            Verify("хибний розвʼязок: тривога піднялась, камера 2 працює", last != null && last.Shared.Alarm > 0f && last.View is HackerView hw && hw.CamerasOff[1] <= 0f && hw.HackLockLeft > 0f);
            board.PointerDown(new Vector2(HackBoardPainter.CancelRect.center.x, HackBoardPainter.CancelRect.center.y));
            screen.TapTarget(1);
            yield return new WaitForSecondsRealtime(0.3f);
            Verify("під час блокування тап показує відлік, а не нову головоломку", board.Mode == HackMode.Locked);
            yield return Shot("h16_hack_locked");
        }

        // ------------------------------------------------------------------ клієнт (Тінь)

        private static IEnumerator ClientFlow()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            GameState state = GameState.Instance;
            state.SetMyName("Тінь");
            ScreenRouter.Instance.ShowJoin();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("c01_join");
            bool shotFailed = false;
            for (int attempt = 0; attempt < 8 && state.Phase != GamePhase.Lobby; attempt++)
            {
                state.JoinByCode(Code);
                yield return new WaitForSecondsRealtime(0.8f);
                if (attempt == 0)
                {
                    yield return Shot("c02_searching");
                }

                yield return Until(() => state.Net != NetStatus.Searching && state.Net != NetStatus.Connecting, 40f, "кінець пошуку");
                if (state.Net == NetStatus.Failed)
                {
                    if (!shotFailed)
                    {
                        shotFailed = true;
                        yield return Shot("c03_not_found");
                    }

                    state.DismissNetMessage();
                    yield return new WaitForSecondsRealtime(1f);
                }
            }

            Verify("клієнт зайшов у лобі за кодом", state.Phase == GamePhase.Lobby && state.Net == NetStatus.Online);
            state.RequestPickRole(RoleId.Shadow);
            yield return new WaitForSecondsRealtime(1.2f);
            Verify("роль Тіні закріплено хостом", state.MyRole == RoleId.Shadow);
            yield return Shot("c04_lobby_client");
            yield return Until(() => state.Phase == GamePhase.Heist, 80f, "хост почав справу");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("c05_heist_shadow");

            // Обрив: гасимо NetworkManager клієнта, як зникла б мережа.
            Log("рву зв'язок");
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }
            else
            {
                Verify("є NetworkManager, який можна обірвати", false);
            }

            yield return new WaitForSecondsRealtime(0.7f); // у Wi-Fi повернення швидке: обрив видно лише короткий час
            Verify("гра показує обрив", state.Net == NetStatus.Lost);
            yield return Shot("c06_lost");
            yield return Until(() => state.Net == NetStatus.Online, 45f, "повернення в кімнату");
            Verify("повернувся на ту саму роль", state.MyRole == RoleId.Shadow && state.Phase == GamePhase.Heist);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("c07_back");

            yield return Until(() => state.Net == NetStatus.Ended, 70f, "хост вийшов");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("c08_host_left");
            state.LeaveRoom();
            yield return new WaitForSecondsRealtime(0.8f);
            Verify("після «У меню» клієнт у меню", state.Phase == GamePhase.Menu && state.Net == NetStatus.Offline);
            yield return Shot("c09_menu_after");
            yield return Quit();
        }
    }

    /// <summary>Носій корутин автоводія.</summary>
    public sealed class UiDriveRunner : MonoBehaviour
    {
    }
}
