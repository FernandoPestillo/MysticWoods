using UnityEngine;
using UnityEngine.EventSystems;

namespace MysticWoods.UI
{
    public sealed class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private CanvasGroup focusDecoration;
        private bool hovered, selected, pressed;
        private void Update()
        {
            bool focused = hovered || selected;
            float blend = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale,
                Vector3.one * (pressed ? 0.98f : focused ? 1.025f : 1), blend);
            focusDecoration.alpha = Mathf.Lerp(focusDecoration.alpha, focused ? 1 : 0, blend);
        }
        public void OnPointerEnter(PointerEventData data)
        {
            hovered = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public void OnPointerExit(PointerEventData data) => hovered = false;
        public void OnSelect(BaseEventData data) => selected = true;
        public void OnDeselect(BaseEventData data) { selected = false; pressed = false; }
        public void OnPointerDown(PointerEventData data) => pressed = true;
        public void OnPointerUp(PointerEventData data) => pressed = false;
        private void OnDisable()
        {
            hovered = selected = pressed = false;
            transform.localScale = Vector3.one;
            if (focusDecoration != null) focusDecoration.alpha = 0;
        }
    }
}
