using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Banda.Backdrop
{
    /// <summary>
    /// Неонова вивіска «БАНДА»: літери випадково збоять (4-та, «Д», — стара трубка, частіше).
    /// Світло вивіски, відсвіт на даху й рожевий відблиск збоять разом з літерами.
    /// </summary>
    public sealed class NeonSign : MonoBehaviour
    {
        [Serializable]
        public struct Letter
        {
            public SpriteRenderer Renderer;
            [Tooltip("Імовірність збою за секунду.")]
            public float GlitchChance;
        }

        [SerializeField] private Letter[] _letters = Array.Empty<Letter>();
        [SerializeField] private Light2D _signLight;
        [SerializeField] private Light2D _bounceLight;
        [SerializeField] private SpriteRenderer _roofGlow;
        [Tooltip("Рожеве марево довкола вивіски (адитивний спрайт).")]
        [SerializeField] private SpriteRenderer _halo;

        private float[] _glitch = Array.Empty<float>();
        private float _signBase;
        private float _bounceBase;
        private float _haloBase;

        private void Awake()
        {
            _glitch = new float[_letters.Length];
            _signBase = _signLight != null ? _signLight.intensity : 0f;
            _bounceBase = _bounceLight != null ? _bounceLight.intensity : 0f;
            _haloBase = _halo != null ? _halo.color.a : 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float lit = 0f;
            for (int i = 0; i < _letters.Length; i++)
            {
                SpriteRenderer letter = _letters[i].Renderer;
                if (letter == null)
                {
                    continue;
                }

                float alpha = 1f;
                if (_glitch[i] > 0f)
                {
                    _glitch[i] -= dt;
                    alpha = UnityEngine.Random.value < 0.55f ? 0.15f : 1f;
                }
                else if (UnityEngine.Random.value < dt * _letters[i].GlitchChance)
                {
                    _glitch[i] = UnityEngine.Random.Range(0.1f, 0.5f);
                }

                Color c = letter.color;
                c.a = alpha;
                letter.color = c;
                lit += alpha;
            }

            float level = lit / Mathf.Max(1f, _letters.Length);
            if (_roofGlow != null)
            {
                Color glow = _roofGlow.color;
                glow.a = Mathf.Lerp(0.7f, 1f, level);
                _roofGlow.color = glow;
            }

            if (_signLight != null)
            {
                _signLight.intensity = _signBase * (0.55f + 0.45f * level);
            }

            if (_bounceLight != null)
            {
                _bounceLight.intensity = _bounceBase * (0.5f + 0.5f * level);
            }

            if (_halo != null)
            {
                Color halo = _halo.color;
                halo.a = _haloBase * level;
                _halo.color = halo;
            }
        }
    }
}
