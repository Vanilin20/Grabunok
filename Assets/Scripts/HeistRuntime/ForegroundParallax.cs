using UnityEngine;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Передній план (стельові лампи, вивіски, короби): об'єкт ближче до камери, ніж підлога, тож зсувається швидше
    /// за світ — легкий паралакс. Працює після камери (HeistWorldView.LateUpdate), тому порядок виконання пізніший.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class ForegroundParallax : MonoBehaviour
    {
        [Tooltip("Сила паралаксу: 0 — як підлога, 0.15 — стеля трохи ближче до камери.")]
        [SerializeField] private float _factor = 0.15f;

        private Vector3 _anchor;
        private bool _hasAnchor;
        private Camera _camera;

        private void OnEnable()
        {
            if (!_hasAnchor)
            {
                _anchor = transform.localPosition;
                _hasAnchor = true;
            }

            _camera = Camera.main;
        }

        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>Поставити об'єкт відносно поточної камери (викликається й зі знімків, де LateUpdate не біжить).</summary>
        public void Apply()
        {
            if (_camera == null || transform.parent == null)
            {
                return;
            }

            Vector3 world = transform.parent.TransformPoint(_anchor);
            Vector3 offset = world - _camera.transform.position;
            offset.z = 0f;
            Vector3 shifted = world + offset * _factor;
            // Ціле піксельне положення: передній план не «плаває» між пікселями.
            shifted.x = Mathf.Round(shifted.x);
            shifted.y = Mathf.Round(shifted.y);
            transform.position = new Vector3(shifted.x, shifted.y, transform.position.z);
        }
    }
}
