using System.Collections.Generic;
using System.Text;
using Banda.Backdrop;
using TMPro;
using UnityEngine;

namespace Banda.EditorTools
{
    /// <summary>
    /// Звірка меню з рендером Godot-версії (docs/art/godot_menu_reference.png):
    /// знімок у тому самому стані (ім'я «Іван», паралакс у нулі), середні кольори ключових зон,
    /// склейка Unity | Godot поруч і зменшений рендер «як на телефоні». Викликається в Play.
    /// </summary>
    public static class BandaMenuCompare
    {
        public const string Reference = "docs/art/godot_menu_reference.png";

        /// <summary>Зони кадру 1280×720 (x, y від верхнього лівого кута, ширина, висота).</summary>
        private static readonly Dictionary<string, RectInt> Zones = new Dictionary<string, RectInt>
        {
            { "небо біля вивіски", new RectInt(380, 40, 60, 100) },
            { "каркас вивіски", new RectInt(450, 150, 380, 70) },
            { "верх будинку", new RectInt(430, 225, 420, 40) },
            { "білборд", new RectInt(300, 470, 340, 110) },
            { "ватажок", new RectInt(525, 560, 70, 140) },
            { "хакерка", new RectInt(430, 560, 60, 130) },
            { "двері з лампою", new RectInt(1205, 430, 75, 100) },
            { "підлога даху", new RectInt(700, 640, 480, 70) },
            { "будинки праворуч", new RectInt(860, 400, 330, 180) },
            { "будинки ліворуч", new RectInt(0, 380, 130, 180) },
            { "літери вивіски", new RectInt(455, 60, 375, 75) },
        };

        public static string Run(string version)
        {
            GameObject nameEdit = GameObject.Find("NameEdit");
            if (nameEdit != null)
            {
                nameEdit.GetComponent<TMP_InputField>().text = "Іван";
            }

            BackdropParallax parallax = Object.FindFirstObjectByType<BackdropParallax>();
            if (parallax != null)
            {
                parallax.Frozen = true;
            }

            string unity = $"docs/art/compare/unity_menu_{version}.png";
            BandaCapture.Capture(unity, 1280, 720);
            BandaCapture.SideBySide(unity, Reference, $"docs/art/compare/menu_{version}_unity_vs_godot.png");
            BandaCapture.Capture($"docs/art/compare/unity_menu_{version}_phone.png", 640, 360);
            BandaCapture.Downscale($"docs/art/compare/unity_menu_{version}_phone.png",
                $"docs/art/compare/unity_menu_{version}_phone_half.png", 0.5f);

            StringBuilder report = new StringBuilder();
            foreach (KeyValuePair<string, RectInt> zone in Zones)
            {
                Color g = BandaCapture.AverageColor(Reference, zone.Value);
                Color u = BandaCapture.AverageColor(unity, zone.Value);
                report.AppendLine($"{zone.Key}: Godot {Rgb(g)}  Unity {Rgb(u)}");
            }

            return report.ToString();
        }

        private static string Rgb(Color c)
        {
            return $"({Mathf.RoundToInt(c.r * 255)},{Mathf.RoundToInt(c.g * 255)},{Mathf.RoundToInt(c.b * 255)})";
        }
    }
}
