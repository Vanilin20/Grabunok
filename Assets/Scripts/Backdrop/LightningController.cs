using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Banda.Backdrop
{
    /// <summary>
    /// Блискавка раз на 20–30 с: подвійний спалах, повільне згасання, грім із затримкою 1–3 с.
    /// Спалах освітлює місто й дах через глобальне світло (банда стає силуетами),
    /// а небо й далеке місто (неосвітлювані спрайти) — через адитивну заливку неба.
    /// </summary>
    public sealed class LightningController : MonoBehaviour
    {
        [SerializeField] private Light2D _globalLight;
        [SerializeField] private SpriteRenderer _bolt;
        [SerializeField] private SpriteRenderer _skyFlash;
        [SerializeField] private AudioSource _thunder;
        [SerializeField] private Vector2 _boltXRange = new Vector2(-380f, -60f);
        [SerializeField] private float _skyTop = 180f;
        [SerializeField] private Color _flashColor = new Color(1f, 1f, 1f);
        [SerializeField] private float _flashIntensity = 1.6f;

        private Color _nightColor;
        private float _nightIntensity;
        private float _wait;
        private float _t = -1f;

        private void Awake()
        {
            _nightColor = _globalLight.color;
            _nightIntensity = _globalLight.intensity;
            _wait = Random.Range(20f, 30f);
            _bolt.enabled = false;
            SetSkyFlash(0f);
        }

        private void Update()
        {
            if (_t < 0f)
            {
                _wait -= Time.deltaTime;
                if (_wait <= 0f)
                {
                    Strike();
                }

                return;
            }

            _t += Time.deltaTime;
            float flash = 0f;
            if (_t < 0.06f || (_t > 0.12f && _t < 0.2f))
            {
                flash = 1f;
            }
            else if (_t >= 0.2f)
            {
                flash = Mathf.Max(0f, 1f - (_t - 0.2f) / 0.6f);
            }

            _bolt.enabled = _t < 0.2f;
            _globalLight.color = Color.Lerp(_nightColor, _flashColor, flash);
            _globalLight.intensity = Mathf.Lerp(_nightIntensity, _flashIntensity, flash);
            SetSkyFlash(flash);
            if (_t > 0.8f)
            {
                _t = -1f;
                _globalLight.color = _nightColor;
                _globalLight.intensity = _nightIntensity;
                SetSkyFlash(0f);
                _wait = Random.Range(20f, 30f);
            }
        }

        /// <summary>Вдарити блискавкою зараз (для перевірки й скріншотів).</summary>
        [ContextMenu("Strike now")]
        public void Strike()
        {
            _t = 0f;
            // Ліва половина неба — не за панеллю меню.
            Vector3 p = _bolt.transform.localPosition;
            _bolt.transform.localPosition = new Vector3(Mathf.Round(Random.Range(_boltXRange.x, _boltXRange.y)), _skyTop, p.z);
            if (_thunder != null && _thunder.clip != null)
            {
                StartCoroutine(ThunderAfter(Random.Range(1f, 3f)));
            }
        }

        private IEnumerator ThunderAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _thunder.Play();
        }

        private void SetSkyFlash(float flash)
        {
            if (_skyFlash == null)
            {
                return;
            }

            Color c = _skyFlash.color;
            c.a = flash * 0.45f;
            _skyFlash.color = c;
            _skyFlash.enabled = flash > 0f;
        }
    }
}
