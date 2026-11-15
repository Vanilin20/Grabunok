using System.IO;
using Banda.HeistRuntime;
using UnityEditor;
using UnityEngine;

namespace Banda.EditorTools
{
    /// <summary>
    /// Рендер вікна головоломки Хакера в PNG (без сцени й без текстів TMP) — швидкий перегляд арту між ітераціями.
    /// Меню Banda/Хакер/Відрендерити вікно взлому або batch: tools/render_hack_board.sh. Файли — Captures/hack_board/.
    /// </summary>
    public static class HackBoardRender
    {
        private const int Scale = 3;

        [MenuItem("Banda/Хакер/Відрендерити вікно взлому")]
        public static void RenderAll()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "hack_board"));
            Directory.CreateDirectory(dir);
            HackBoardPainter p = new HackBoardPainter();
            HackBoardState s = Puzzle(4, new[] { 2, 0, 3, 1 }, new[] { -1, -1, -1, -1 });
            p.Paint(s);
            Save(p, dir, "1_idle");

            s.SocketOf = new[] { 1, 3, -1, -1 };
            s.Dragging = 2;
            s.DragPos = new Vector2(200f, 118f);
            s.TimeFrac = 0.4f;
            p.Paint(s);
            Save(p, dir, "2_dragging");

            s.SocketOf = new[] { 1, 3, 0, 2 };
            s.Dragging = -1;
            s.CanSubmit = true;
            s.TimeFrac = 0.2f;
            p.Paint(s);
            Save(p, dir, "3_ready");

            s.Mode = HackMode.Solved;
            s.Flash = 1f;
            p.Paint(s);
            Save(p, dir, "4_solved");

            s.Mode = HackMode.Locked;
            s.Failed = true;
            s.Flash = 1f;
            p.Paint(s);
            Save(p, dir, "5_wrong");

            HackBoardState door = Puzzle(5, new[] { 3, 1, 4, 0, 2 }, new[] { 3, -1, 0, 4, -1 });
            door.TimeFrac = 0.9f;
            p.Paint(door);
            Save(p, dir, "6_door5");
            Debug.Log("[Banda] Вікно взлому відрендерено в " + dir);
        }

        private static HackBoardState Puzzle(int wires, int[] right, int[] socketOf)
        {
            return new HackBoardState { Mode = HackMode.Puzzle, Wires = wires, Right = right, SocketOf = socketOf, Clock = 1.3f, TimeFrac = 0.7f };
        }

        private static void Save(HackBoardPainter painter, string dir, string name)
        {
            Texture2D t = new Texture2D(HackBoardPainter.W, HackBoardPainter.H, TextureFormat.RGBA32, false);
            t.SetPixels32(painter.Pixels);
            t.Apply();
            Texture2D big = new Texture2D(t.width * Scale, t.height * Scale, TextureFormat.RGBA32, false);
            Color32[] src = t.GetPixels32();
            Color32[] dst = new Color32[big.width * big.height];
            for (int y = 0; y < big.height; y++)
            {
                for (int x = 0; x < big.width; x++)
                {
                    dst[y * big.width + x] = src[(y / Scale) * t.width + x / Scale];
                }
            }

            big.SetPixels32(dst);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), big.EncodeToPNG());
            Object.DestroyImmediate(t);
            Object.DestroyImmediate(big);
        }
    }
}
