using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class UIPointerClickEvents : MonoBehaviour, IPointerClickHandler
    {
        [FormerlySerializedAs("OnClick")]
        public UnityEvent<GameObject> OnLeftClick;
        public UnityEvent<GameObject> OnRightClick;
        public UnityEvent OnDoubleClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnLeftClick?.Invoke(gameObject);
                if (eventData.clickCount == 2)
                    OnDoubleClick?.Invoke();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnRightClick?.Invoke(gameObject);
            }
        }
    }
}
