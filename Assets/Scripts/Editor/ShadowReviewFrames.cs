using System.Reflection;
using Banda.Heist;
using Banda.HeistRuntime;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Banda.EditorTools
{
    /// <summary>
    /// Зафіксований тестовий набір кадрів екрана Тіні для рецензій (docs/levels/review_frames.md).
    /// Позиції Тіні й охоронця задаються в симуляції й одразу у вигляді світу (без згладжування), камера рахується
    /// звичайним LateUpdate HeistWorldView (винос і межі арту — як у грі). Охоронець видимий лише тоді, коли гра теж
    /// показала б його Тіні (відстань ≤ ShadowViewRadius). Тривога перед кожним кадром — 0.
    /// Набір НЕ змінюється між ітераціями: це вимірювальний інструмент, а не підгонка під оцінку.
    /// Використання (у Play, справа запущена роллю Тіні): ShadowReviewFrames.CaptureAll("Captures/unity/shadow_set").
    /// </summary>
    public static class ShadowReviewFrames
    {
        public readonly struct Frame
        {
            public readonly string Name;
            public readonly Vec2 Shadow;
            public readonly float ShadowFacing;
            public readonly Vec2 Guard;
            public readonly float GuardFacing;
            public readonly int GuardTarget;

            public Frame(string name, Vec2 shadow, float shadowFacing, Vec2 guard, float guardFacing, int guardTarget)
            {
                Name = name;
                Shadow = shadow;
                ShadowFacing = shadowFacing;
                Guard = guard;
                GuardFacing = guardFacing;
                GuardTarget = guardTarget;
            }
        }

        private const float North = -Mathf.PI / 2f; // симуляція: Y униз
        private const float South = Mathf.PI / 2f;
        private const float East = 0f;
        private const float West = Mathf.PI;

        /// <summary>
        /// П'ять кадрів. Маршрут охоронця (HeistLevel.CornerStore): 0 (3,10) → 1 (14,10) → 2 (14,2) → 3 (1,2) → 4 (1,10).
        /// GuardTarget — індекс точки, до якої він іде (як у симуляції).
        /// </summary>
        public static readonly Frame[] Frames =
        {
            new Frame("1_start", HeistLevel.Center(8, 12), North, HeistLevel.Center(3, 10), East, 1),
            new Frame("2_aisle", HeistLevel.Center(5, 5), North, new Vec2(104f, 40f), West, 3),
            new Frame("3_hall_right", HeistLevel.Center(16, 8), West, new Vec2(232f, 120f), North, 2),
            new Frame("4_counter", HeistLevel.Center(12, 10), North, new Vec2(24f, 104f), South, 4),
            new Frame("5_backroom", HeistLevel.Center(22, 2), East, new Vec2(120f, 40f), West, 3),
        };

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static string CaptureAll(string prefix)
        {
            string log = string.Empty;
            foreach (Frame f in Frames)
            {
                log += Capture(f, $"{prefix}_{f.Name}.png") + "\n";
            }

            return log;
        }

        public static string Capture(Frame f, string path)
        {
            HeistDirector director = HeistDirector.Instance;
            LocalTransport transport = director != null ? director.Transport as LocalTransport : null;
            if (transport == null)
            {
                return "немає справи (запусти справу роллю Тіні в Play)";
            }

            HeistSim sim = transport.Session.Sim;
            System.Type simType = typeof(HeistSim);
            simType.GetProperty("ShadowPos").SetValue(sim, f.Shadow);
            simType.GetProperty("GuardPos").SetValue(sim, f.Guard);
            simType.GetProperty("GuardFacing").SetValue(sim, f.GuardFacing);
            simType.GetProperty("Alarm").SetValue(sim, 0f);
            simType.GetField("_guardTarget", Any).SetValue(sim, f.GuardTarget);

            HeistWorldView view = Object.FindFirstObjectByType<HeistWorldView>();
            System.Type viewType = typeof(HeistWorldView);
            CharacterSpriteAnimator shadow = (CharacterSpriteAnimator)viewType.GetField("_shadow", Any).GetValue(view);
            CharacterSpriteAnimator guard = (CharacterSpriteAnimator)viewType.GetField("_guard", Any).GetValue(view);
            Light2D flashlight = (Light2D)viewType.GetField("_guardFlashlight", Any).GetValue(view);

            Vector3 shadowWorld = HeistWorldView.ToWorld(f.Shadow);
            shadow.transform.localPosition = shadowWorld;
            viewType.GetField("_shadowTarget", Any).SetValue(view, shadowWorld);
            shadow.SetVelocity(Vector3.zero);
            shadow.Face(f.ShadowFacing);

            bool guardVisible = f.Shadow.DistanceTo(f.Guard) <= HeistSim.ShadowViewRadius;
            guard.gameObject.SetActive(guardVisible);
            if (guardVisible)
            {
                Vector3 guardWorld = HeistWorldView.ToWorld(f.Guard);
                guard.transform.localPosition = guardWorld;
                viewType.GetField("_guardTarget", Any).SetValue(view, guardWorld);
                guard.SetVelocity(Vector3.zero);
                guard.Face(f.GuardFacing);
                flashlight.transform.localRotation = Quaternion.Euler(0f, 0f, -f.GuardFacing * Mathf.Rad2Deg - 90f);
                Vector2 hand = guard.HandPoint;
                flashlight.transform.localPosition = new Vector3(hand.x, hand.y, flashlight.transform.localPosition.z);
            }

            // Камера, сортування й rim — тим самим кодом, що в грі.
            viewType.GetMethod("LateUpdate", Any).Invoke(view, null);
            // Передній план з паралаксом рахується після камери — у знімку LateUpdate не біжить, тож викликаємо явно.
            foreach (ForegroundParallax fg in Object.FindObjectsByType<ForegroundParallax>(FindObjectsSortMode.None))
            {
                fg.Apply();
            }
            shadow.SendMessage("Update");
            if (guardVisible)
            {
                guard.SendMessage("Update");
            }

            string result = BandaCapture.Capture(path, 1280, 720);
            Vector3 cam = Camera.main.transform.position;
            return $"{f.Name}: {result} | охоронець {(guardVisible ? "видно" : "не видно")} | камера ({cam.x:0.0}, {cam.y:0.0}) | тривога {sim.Alarm:0}";
        }
    }
}
