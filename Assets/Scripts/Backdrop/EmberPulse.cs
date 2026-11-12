using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Banda.Backdrop
{
    /// <summary>Вогник цигарки: повільні затяжки — світло різко спалахує й довго згасає.</summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class EmberPulse : MonoBehaviour
    {
        [SerializeField] private float _min = 0.5f;
        [SerializeField] private float _boost = 0.7f;
        [SerializeField] private float _speed = 0.9f;

        private Light2D _light;
        private float _scale;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            // Інтенсивність у сцені задана для «спокійного» стану; затяжка додає зверху.
            _scale = _light.intensity / Mathf.Max(0.01f, _min);
        }

        private void Update()
        {
            float puff = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * _speed)), 4f);
            _light.intensity = (_min + _boost * puff) * _scale;
        }
    }
}
