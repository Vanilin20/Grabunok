using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Banda.HeistRuntime
{
    /// <summary>
    /// Передає дотики з UI-області (диск сейфа, зона джойстика) логіці екрана ролі, яка живе на завжди активному обʼєкті.
    /// </summary>
    public sealed class PointerRelay : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public event Action<PointerEventData> Down;
        public event Action<PointerEventData> Dragged;
        public event Action<PointerEventData> Up;

        public void OnPointerDown(PointerEventData eventData) => Down?.Invoke(eventData);

        public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData);

        public void OnPointerUp(PointerEventData eventData) => Up?.Invoke(eventData);
    }
}
