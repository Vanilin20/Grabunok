using UnityEngine;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Анімація персонажа згори зі стрічки 7×4 (рядки: вниз, праворуч, вгору, ліворуч; стовпці: idle + 6 кадрів ходьби).
    /// Напрям — за рухом або за погляду від хоста; швидкість кадрів — за швидкістю руху.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CharacterSpriteAnimator : MonoBehaviour
    {
        private const int Columns = 8; // idle, вдих, 6 кадрів ходьби
        private const int WalkFirst = 2;
        private const float BreathFps = 1.4f;
        private const int Rows = 4;
        private const int FrameSize = 16;

        [SerializeField] private Texture2D _sheet;
        [SerializeField] private float _walkFps = 10f;
        [SerializeField] private float _moveThreshold = 4f;
        [Tooltip("Прозорість, коли персонаж схований (Тінь за стелажем).")]
        [SerializeField] private float _hiddenAlpha = 0.45f;

        private SpriteRenderer _renderer;
        private Sprite[,] _frames;
        private int _direction;
        private float _time;
        private float _speed;
        private float _breath;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _frames = new Sprite[Rows, Columns];
            if (_sheet == null)
            {
                Debug.LogError($"[CharacterSpriteAnimator] {name}: немає стрічки кадрів.");
                enabled = false;
                return;
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    // Рядок 0 у PNG — угорі, а координати текстури Unity — знизу.
                    Rect rect = new Rect(col * FrameSize, _sheet.height - (row + 1) * FrameSize, FrameSize, FrameSize);
                    _frames[row, col] = Sprite.Create(_sheet, rect, new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                }
            }

            _renderer.sprite = _frames[0, 0];
            _breath = Random.value * 2f;
        }

        private void OnDestroy()
        {
            if (_frames == null)
            {
                return;
            }

            foreach (Sprite s in _frames)
            {
                if (s != null)
                {
                    Destroy(s);
                }
            }
        }

        /// <summary>Повернути обличчям за кутом (радіани, Y униз, як у симуляції).</summary>
        public void Face(float radians)
        {
            float deg = radians * Mathf.Rad2Deg;
            // 0° — праворуч, 90° — вниз (Y униз), 180° — ліворуч, -90° — вгору.
            if (deg >= -45f && deg < 45f)
            {
                _direction = 1;
            }
            else if (deg >= 45f && deg < 135f)
            {
                _direction = 0;
            }
            else if (deg >= -135f && deg < -45f)
            {
                _direction = 2;
            }
            else
            {
                _direction = 3;
            }
        }

        /// <summary>Швидкість у світі Unity (Y угору) — для вибору кадру й напрямку.</summary>
        public void SetVelocity(Vector3 velocity)
        {
            _speed = velocity.magnitude;
            if (_speed > _moveThreshold)
            {
                Face(Mathf.Atan2(-velocity.y, velocity.x));
            }
        }

        public void SetHidden(bool hidden)
        {
            Color c = _renderer.color;
            c.a = hidden ? _hiddenAlpha : 1f;
            _renderer.color = c;
        }

        private void Update()
        {
            if (_frames == null)
            {
                return;
            }

            int col;
            if (_speed > _moveThreshold)
            {
                _time += Time.deltaTime * _walkFps;
                col = WalkFirst + Mathf.FloorToInt(_time) % (Columns - WalkFirst);
            }
            else
            {
                // Дихання на місці: повільно між idle і вдихом, зі зсувом фази в кожного персонажа.
                _breath += Time.deltaTime * BreathFps;
                col = Mathf.FloorToInt(_breath) % 2;
            }

            _renderer.sprite = _frames[_direction, col];
        }
    }
}
