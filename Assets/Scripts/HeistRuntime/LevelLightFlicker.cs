using Banda.Heist;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Banda.HeistRuntime
{
    /// <summary>Мерехтіння світла рівня (LightFlicker з HeistLevel): гудіння люмінесцентної лампи чи лампа, що доживає.</summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class LevelLightFlicker : MonoBehaviour
    {
        [SerializeField] private LightFlicker _kind;

        private Light2D _light;
        private float _base;
        private float _off;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _base = _light.intensity;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (_kind)
            {
                case LightFlicker.Buzz:
                    _light.intensity = _base * (0.93f + 0.07f * Mathf.Sin(Time.time * 47f)) * (Random.value < dt * 0.2f ? 0.5f : 1f);
                    break;
                case LightFlicker.Dying:
                    if (_off > 0f)
                    {
                        _off -= dt;
                        _light.intensity = _base * 0.12f;
                    }
                    else
                    {
                        _light.intensity = _base * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, 0f));
                        if (Random.value < dt * 0.6f)
                        {
                            _off = Random.Range(0.05f, 0.25f);
                        }
                    }

                    break;
                default:
                    _light.intensity = _base;
                    break;
            }
        }
    }
}
