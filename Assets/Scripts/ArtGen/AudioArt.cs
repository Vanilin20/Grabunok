using System;
using System.Collections.Generic;
using System.IO;

namespace Banda.ArtGen
{
    /// <summary>
    /// Звуки справи, синтезовані кодом (WAV 22 050 Гц, моно, 16 біт): клац диска, удар влучання, крок,
    /// гудіння ламп (петля), сирена тривоги (петля), двері, вимкнення камери.
    /// Claude звуку не чує — параметри підібрано за описом; оцінка — за Іваном (CLAUDE.md, «Перевірка якості»).
    /// </summary>
    public static class AudioArt
    {
        private const int Rate = 22050;

        public static List<string> GenerateAll(string projectRoot)
        {
            string dir = Path.Combine(projectRoot, "Assets", "Audio");
            List<string> written = new List<string>();
            Save(dir, "safe_tick.wav", Click(0.025f, 2600f, 0.35f, 1), written);
            Save(dir, "safe_hit.wav", Thunk(), written);
            Save(dir, "step_soft.wav", Step(), written);
            Save(dir, "lamp_hum.wav", Hum(), written);
            Save(dir, "alarm_loop.wav", Siren(), written);
            Save(dir, "door_toggle.wav", Door(), written);
            Save(dir, "cam_off.wav", CamOff(), written);
            return written;
        }

        /// <summary>Сухий металевий клац: короткий шумовий імпульс + затухаючий тон.</summary>
        private static float[] Click(float seconds, float freq, float gain, int seed)
        {
            int n = (int)(Rate * seconds);
            float[] s = new float[n];
            Random rng = new Random(seed);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = MathF.Exp(-t * 180f);
                float noise = (float)(rng.NextDouble() * 2 - 1) * MathF.Exp(-t * 600f);
                s[i] = gain * (env * MathF.Sin(2f * MathF.PI * freq * t) * 0.6f + noise * 0.5f);
            }

            return s;
        }

        /// <summary>Глухий важкий удар «влучив цифру»: низький тон з клацом зверху.</summary>
        private static float[] Thunk()
        {
            int n = (int)(Rate * 0.18f);
            float[] s = new float[n];
            float[] click = Click(0.03f, 1800f, 0.4f, 2);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float low = MathF.Sin(2f * MathF.PI * (110f - t * 120f) * t) * MathF.Exp(-t * 22f);
                s[i] = 0.7f * low + (i < click.Length ? click[i] : 0f);
            }

            return s;
        }

        /// <summary>М'який крок кедом по лінолеуму: фільтрований шум з коротким наростанням.</summary>
        private static float[] Step()
        {
            int n = (int)(Rate * 0.09f);
            float[] s = new float[n];
            Random rng = new Random(3);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = MathF.Min(1f, t * 400f) * MathF.Exp(-t * 45f);
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.18f;
                s[i] = lp * env * 0.9f;
            }

            return s;
        }

        /// <summary>Гудіння люмінесцентної лампи 100 Гц з гармоніками; рівно 1 с — безшовна петля.</summary>
        private static float[] Hum()
        {
            float[] s = new float[Rate];
            for (int i = 0; i < Rate; i++)
            {
                float t = i / (float)Rate;
                s[i] = 0.08f * MathF.Sin(2f * MathF.PI * 100f * t) + 0.04f * MathF.Sin(2f * MathF.PI * 200f * t)
                       + 0.02f * MathF.Sin(2f * MathF.PI * 300f * t + 0.5f);
            }

            return s;
        }

        /// <summary>Сирена тривоги: тон гуляє 600–900 Гц, 2 с — безшовна петля.</summary>
        private static float[] Siren()
        {
            int n = Rate * 2;
            float[] s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 750f + 150f * MathF.Sin(2f * MathF.PI * 0.5f * t);
                phase += 2f * MathF.PI * f / Rate;
                float square = MathF.Sin(phase) > 0f ? 1f : -1f;
                s[i] = 0.18f * (0.6f * MathF.Sin(phase) + 0.4f * square);
            }

            return s;
        }

        private static float[] Door()
        {
            int n = (int)(Rate * 0.35f);
            float[] s = new float[n];
            float[] latch = Click(0.04f, 900f, 0.5f, 4);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float creak = MathF.Sin(2f * MathF.PI * (320f + 80f * MathF.Sin(t * 40f)) * t) * MathF.Exp(-t * 9f) * 0.25f;
                s[i] = creak + (i < latch.Length ? latch[i] : 0f);
            }

            return s;
        }

        private static float[] CamOff()
        {
            int n = (int)(Rate * 0.25f);
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 1400f - t * 4000f; // «вжух» униз — камера гасне
                s[i] = 0.25f * MathF.Sin(2f * MathF.PI * f * t) * MathF.Exp(-t * 10f);
            }

            return s;
        }

        private static void Save(string dir, string file, float[] samples, List<string> written)
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, file);
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + bytes);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(bytes);
                foreach (float v in samples)
                {
                    w.Write((short)(Math.Clamp(v, -1f, 1f) * 32000f));
                }
            }

            written.Add(path);
        }
    }
}
