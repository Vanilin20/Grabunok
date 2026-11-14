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

        [SerializeField] private Texture2D _sheet;
        [Tooltip("Розмір кадру в пікселях: 16 — старі стрічки ArtGen, 32 — персонажі з ключових поз PixelLab.")]
        [SerializeField] private int _frameSize = 16;
        [Tooltip("Опора по вертикалі (0 — низ кадру). Для 32 px — біля ніг, щоб персонаж стояв на своїй клітинці.")]
        [SerializeField, Range(0f, 1f)] private float _pivotY = 0.5f;
        [Tooltip("Точка в руці (лінза ліхтарика) для кожного напрямку — вниз, праворуч, вгору, ліворуч; пікселі від опори, Y угору.")]
        [SerializeField] private Vector2[] _handPoints = new Vector2[0];
        [Tooltip("Ореол: силует, розширений на 1 px (та сама сітка кадрів). Малюється під персонажем кольором найближчого світла.")]
        [SerializeField] private Texture2D _haloSheet;
        [SerializeField] private SpriteRenderer _halo;

        /// <summary>Поточний напрямок: 0 — вниз, 1 — праворуч, 2 — вгору, 3 — ліворуч.</summary>
        public int Direction => _direction;

        /// <summary>Точка в руці для поточного напрямку (нуль, якщо не задано).</summary>
        public Vector2 HandPoint => _direction < _handPoints.Length ? _handPoints[_direction] : Vector2.zero;
        [SerializeField] private float _walkFps = 10f;
        [SerializeField] private float _moveThreshold = 4f;
        [Tooltip("Прозорість, коли персонаж схований (Тінь за стелажем).")]
        [SerializeField] private float _hiddenAlpha = 0.45f;

        private SpriteRenderer _renderer;
        private Sprite[,] _frames;
        private Sprite[,] _haloFrames;
        private Color _rim = Color.clear;
        private bool _hidden;
        private int _direction;
        private float _time;
        private float _speed;
        private float _breath;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _frames = new Sprite[Rows, Columns];
            if (_sheet == null || _frameSize <= 0 || _sheet.width < Columns * _frameSize || _sheet.height < Rows * _frameSize)
            {
                Debug.LogError($"[CharacterSpriteAnimator] {name}: немає стрічки кадрів або вона менша за {Columns}×{Rows} кадрів по {_frameSize} px.");
                enabled = false;
                return;
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    // Рядок 0 у PNG — угорі, а координати текстури Unity — знизу.
                    Rect rect = new Rect(col * _frameSize, _sheet.height - (row + 1) * _frameSize, _frameSize, _frameSize);
                    _frames[row, col] = Sprite.Create(_sheet, rect, new Vector2(0.5f, _pivotY), 1f, 0, SpriteMeshType.FullRect);
                }
            }

            if (_haloSheet != null && _halo != null)
            {
                _haloFrames = new Sprite[Rows, Columns];
                for (int row = 0; row < Rows; row++)
                {
                    for (int col = 0; col < Columns; col++)
                    {
                        Rect rect = new Rect(col * _frameSize, _haloSheet.height - (row + 1) * _frameSize, _frameSize, _frameSize);
                        _haloFrames[row, col] = Sprite.Create(_haloSheet, rect, new Vector2(0.5f, _pivotY), 1f, 0, SpriteMeshType.FullRect);
                    }
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

            if (_haloFrames != null)
            {
                foreach (Sprite s in _haloFrames)
                {
                    if (s != null)
                    {
                        Destroy(s);
                    }
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
            _hidden = hidden;
            Color c = _renderer.color;
            c.a = hidden ? _hiddenAlpha : 1f;
            _renderer.color = c;
            ApplyRim();
        }

        /// <summary>Колір кільця-ореолу (rim від найближчого джерела); альфа — сила світла.</summary>
        public void SetRim(Color rim)
        {
            _rim = rim;
            ApplyRim();
        }

        /// <summary>Порядок малювання персонажа; ореол — одразу під ним.</summary>
        public void SetSortingOrder(int order)
        {
            _renderer.sortingOrder = order;
            if (_halo != null)
            {
                _halo.sortingOrder = order - 1;
            }
        }

        private void ApplyRim()
        {
            if (_halo == null)
            {
                return;
            }

            Color c = _rim;
            c.a *= _hidden ? _hiddenAlpha : 1f;
            _halo.color = c;
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
            if (_haloFrames != null)
            {
                _halo.sprite = _haloFrames[_direction, col];
            }
        }
    }
}
