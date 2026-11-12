using UnityEngine;

namespace Banda.HeistRuntime
{
    /// <summary>Діод камери: блимає червоним (у світі Тіні стан камери невідомий — блимає завжди).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LedBlink : MonoBehaviour
    {
        [SerializeField] private float _period = 1.2f;

        private SpriteRenderer _renderer;
        private float _phase;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _phase = Random.value * _period;
        }

        private void Update()
        {
            _renderer.enabled = Mathf.Repeat(Time.time + _phase, _period) < _period * 0.25f;
        }
    }
}
