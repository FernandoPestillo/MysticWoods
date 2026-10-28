using UnityEngine;

namespace MysticWoods.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MenuPanelFade : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float duration = 0.2f;
        private CanvasGroup group;
        private void OnEnable() { group = GetComponent<CanvasGroup>(); group.alpha = 0; }
        private void Update() => group.alpha = Mathf.MoveTowards(group.alpha, 1, Time.unscaledDeltaTime / duration);
    }
}
