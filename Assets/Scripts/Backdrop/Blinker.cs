using UnityEngine;

namespace Banda.Backdrop
{
    /// <summary>Червоні вогні на антенах далеких будинків: короткий спалах раз на період.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Blinker : MonoBehaviour
    {
        private const float OnTime = 0.18f;

        private SpriteRenderer _renderer;
        private float _phase;
        private float _period;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _phase = Random.value * 3f;
            _period = Random.Range(1.4f, 2.6f);
        }

        private void Update()
        {
            _renderer.enabled = Mathf.Repeat(Time.time + _phase, _period) < OnTime;
        }
    }
}
