using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Banda.Backdrop
{
    /// <summary>
    /// Паралакс шарів міста: від нахилу телефона (гравітація), на ПК — від миші.
    /// Зсув округлюється до цілих пікселів, щоб піксель-арт не «плив».
    /// </summary>
    public sealed class BackdropParallax : MonoBehaviour
    {
        [Serializable]
        public struct Layer
        {
            public Transform Target;
            [Tooltip("Максимальний зсув шару в пікселях: що ближче план, то більше.")]
            public float Amount;
        }

        [SerializeField] private Layer[] _layers = Array.Empty<Layer>();
        [SerializeField] private float _smoothing = 3f;

        private Vector3[] _basePositions = Array.Empty<Vector3>();
        private Vector2 _current;

        private void Awake()
        {
            _basePositions = new Vector3[_layers.Length];
            for (int i = 0; i < _layers.Length; i++)
            {
                _basePositions[i] = _layers[i].Target != null ? _layers[i].Target.localPosition : Vector3.zero;
            }
        }

        private void OnEnable()
        {
            if (GravitySensor.current != null)
            {
                InputSystem.EnableDevice(GravitySensor.current);
            }
        }

        private void OnDisable()
        {
            if (GravitySensor.current != null && GravitySensor.current.enabled)
            {
                InputSystem.DisableDevice(GravitySensor.current);
            }
        }

        /// <summary>Заморозити шари в нейтральному положенні (знімки для порівняння з макетом).</summary>
        public bool Frozen
        {
            get => _frozen;
            set
            {
                _frozen = value;
                if (_frozen)
                {
                    // Застосовуємо одразу: знімок може бути зроблений до наступного кадру.
                    _current = Vector2.zero;
                    Apply();
                }
            }
        }

        private bool _frozen;

        private void Update()
        {
            Vector2 target = _frozen ? Vector2.zero : ReadTarget();
            _current = Vector2.Lerp(_current, target, 1f - Mathf.Exp(-_smoothing * Time.unscaledDeltaTime));
            Apply();
        }

        private void Apply()
        {
            for (int i = 0; i < _layers.Length; i++)
            {
                Transform layer = _layers[i].Target;
                if (layer == null)
                {
                    continue;
                }

                Vector2 offset = _current * _layers[i].Amount;
                Vector3 basePos = _basePositions[i];
                // У Unity вісь Y дивиться вгору — тому знак по Y протилежний до Godot.
                layer.localPosition = new Vector3(Mathf.Round(basePos.x - offset.x), Mathf.Round(basePos.y + offset.y), basePos.z);
            }
        }

        private static Vector2 ReadTarget()
        {
            GravitySensor gravity = GravitySensor.current;
            if (gravity != null && gravity.enabled)
            {
                // Нахил телефона: вектор гравітації, нормований до 1g.
                // ПЕРЕВІРИТИ НА ТЕЛЕФОНІ: знаки осей Input System можуть відрізнятися від Godot.
                Vector3 g = gravity.gravity.ReadValue();
                if (g.sqrMagnitude > 0.01f)
                {
                    Vector3 n = g.normalized;
                    return new Vector2(Mathf.Clamp(n.x * 2.45f, -1f, 1f), Mathf.Clamp(-n.y - 0.6f, -1f, 1f));
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = mouse.position.ReadValue();
                // Godot рахував позицію миші згори вниз — перевертаємо Y, щоб рух збігався.
                Vector2 t = new Vector2(p.x / Screen.width - 0.5f, 0.5f - p.y / Screen.height) * 2f;
                return new Vector2(Mathf.Clamp(t.x, -1f, 1f), Mathf.Clamp(t.y, -1f, 1f));
            }

            return Vector2.zero;
        }
    }
}
