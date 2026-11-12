using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Banda.Backdrop
{
    /// <summary>
    /// Живе світло: «Buzz» — лампа гуде й зрідка просідає, «Neon» — трубка бару зрідка гасне на кадр.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class FlickerLight : MonoBehaviour
    {
        public enum Kind
        {
            Buzz,
            Neon,
        }

        [SerializeField] private Kind _kind = Kind.Buzz;

        private Light2D _light;
        private float _base;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _base = _light.intensity;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_kind == Kind.Buzz)
            {
                float hum = 0.94f + 0.06f * Mathf.Sin(Time.time * 50f);
                float dip = Random.value < dt * 0.15f ? 0.4f : 1f;
                _light.intensity = _base * hum * dip;
            }
            else
            {
                _light.intensity = _base * (Random.value < dt * 1.2f ? 0.15f : 1f);
            }
        }
    }
}
