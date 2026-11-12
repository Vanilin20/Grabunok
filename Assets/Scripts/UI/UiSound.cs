using UnityEngine;

namespace Banda.UI
{
    /// <summary>
    /// Звуки інтерфейсу: клац на натискання, глухий «тук» на вимкнену кнопку (аналог UiSound з Godot).
    /// Кнопки звертаються сюди через UiButtonFeedback.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class UiSound : MonoBehaviour
    {
        public static UiSound Instance { get; private set; }

        [SerializeField] private AudioClip _click;
        [SerializeField] private AudioClip _deny;

        private AudioSource _source;

        private void Awake()
        {
            Instance = this;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void PlayClick() => Play(_click);

        public void PlayDeny() => Play(_deny);

        private void Play(AudioClip clip)
        {
            if (clip != null)
            {
                _source.PlayOneShot(clip);
            }
        }
    }
}
