using UnityEngine;

namespace Banda.Backdrop
{
    /// <summary>
    /// Покадрова анімація зі стрічки кадрів (idle банди з диханням).
    /// Кадри ріжуться під час запуску з однієї текстури: розмір кадру береться з макета арту.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteSheetAnimator : MonoBehaviour
    {
        [SerializeField] private Texture2D _sheet;
        [SerializeField] private Vector2Int _frameSize = new Vector2Int(48, 76);
        [SerializeField] private float _fps = 3f;
        [SerializeField] private bool _randomStartFrame = true;

        private SpriteRenderer _renderer;
        private Sprite[] _frames = System.Array.Empty<Sprite>();
        private float _time;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_sheet == null || _frameSize.x <= 0 || _frameSize.y <= 0)
            {
                Debug.LogError($"[SpriteSheetAnimator] {name}: немає стрічки кадрів або розміру кадру.");
                enabled = false;
                return;
            }

            int count = _sheet.width / _frameSize.x;
            _frames = new Sprite[count];
            // Опорна точка — верхній лівий кут кадру, як у Godot (centered = false).
            Vector2 pivot = new Vector2(0f, 1f);
            for (int i = 0; i < count; i++)
            {
                Rect rect = new Rect(i * _frameSize.x, _sheet.height - _frameSize.y, _frameSize.x, _frameSize.y);
                _frames[i] = Sprite.Create(_sheet, rect, pivot, 1f, 0, SpriteMeshType.FullRect);
                _frames[i].name = $"{_sheet.name}_{i}";
            }

            // Не дихають синхронно.
            _time = _randomStartFrame ? Random.Range(0, count) / Mathf.Max(0.01f, _fps) : 0f;
            _renderer.sprite = _frames[0];
        }

        private void OnDestroy()
        {
            foreach (Sprite frame in _frames)
            {
                if (frame != null)
                {
                    Destroy(frame);
                }
            }
        }

        private void Update()
        {
            if (_frames.Length == 0)
            {
                return;
            }

            _time += Time.deltaTime;
            int index = Mathf.FloorToInt(_time * _fps) % _frames.Length;
            _renderer.sprite = _frames[index];
        }
    }
}
