using UnityEngine;

namespace Banda.Backdrop
{
    /// <summary>Ліхтарик у руці ватажка ледь гуляє — рука живої людини.</summary>
    public sealed class FlashlightSway : MonoBehaviour
    {
        [SerializeField] private float _baseAngle = -35f;

        private void Update()
        {
            float t = Time.time;
            float angle = _baseAngle + Mathf.Sin(t * 0.7f) * 4f + Mathf.Sin(t * 1.9f) * 1.5f;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
