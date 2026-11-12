using UnityEngine;

namespace Banda.Backdrop
{
    /// <summary>Потяг на віадуці: час від часу проїжджає в випадковий бік.</summary>
    public sealed class TrainPass : MonoBehaviour
    {
        [SerializeField] private GameObject _body;
        [SerializeField] private float _speed = 64f;
        [SerializeField] private float _halfCanvas = 432f;
        [SerializeField] private float _trainLength = 210f;

        private float _wait = 2f;
        private float _direction = 1f;
        private bool _running;

        private void Awake()
        {
            _body.SetActive(false);
        }

        private void Update()
        {
            Vector3 p = transform.localPosition;
            if (!_running)
            {
                _wait -= Time.deltaTime;
                if (_wait > 0f)
                {
                    return;
                }

                _direction = Random.value < 0.5f ? 1f : -1f;
                // Спрайт потяга дзеркалиться разом із напрямком — фари завжди попереду.
                transform.localScale = new Vector3(_direction, 1f, 1f);
                p.x = _direction > 0f ? -_halfCanvas - _trainLength : _halfCanvas + _trainLength;
                _running = true;
                _body.SetActive(true);
            }

            p.x += _speed * _direction * Time.deltaTime;
            transform.localPosition = new Vector3(p.x, p.y, p.z);
            if (Mathf.Abs(p.x) > _halfCanvas + _trainLength + 10f)
            {
                _running = false;
                _body.SetActive(false);
                _wait = Random.Range(5f, 12f);
            }
        }
    }
}
