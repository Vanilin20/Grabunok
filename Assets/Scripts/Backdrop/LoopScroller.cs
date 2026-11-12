using UnityEngine;

namespace Banda.Backdrop
{
    /// <summary>Безкінечна прокрутка шару (хмари): дві копії поруч, зсув по колу на ширину полотна.</summary>
    public sealed class LoopScroller : MonoBehaviour
    {
        [SerializeField] private float _speed = 3f;
        [SerializeField] private float _loopWidth = 864f;

        private float _offset;
        private Vector3 _start;

        private void Awake()
        {
            _start = transform.localPosition;
        }

        private void Update()
        {
            _offset -= _speed * Time.deltaTime;
            if (_offset <= -_loopWidth)
            {
                _offset += _loopWidth;
            }

            transform.localPosition = _start + new Vector3(Mathf.Round(_offset), 0f, 0f);
        }
    }
}
