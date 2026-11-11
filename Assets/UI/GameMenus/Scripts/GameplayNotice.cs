using TMPro;
using UnityEngine;

namespace MysticWoods.UI
{
    public sealed class GameplayNotice : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField, Min(.1f)] private float displayDuration = 3.5f;
        [SerializeField, Min(.01f)] private float fadeDuration = .2f;
        private GameMenuController menus;
        private HeartSystem heart;
        private float remaining;
        private float opacity;

        private void Awake()
        {
            menus = FindAnyObjectByType<GameMenuController>();
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null) heart = player.GetComponent<HeartSystem>();
            panel.blocksRaycasts = panel.interactable = false;
            Clear();
        }

        public void Show(string message)
        {
            if (Time.timeScale <= 0f || (heart != null && heart.Health <= 0)) return;
            // Repeated contact must not keep restarting the same notice.
            if (remaining > 0f && messageLabel.text == message) return;
            messageLabel.text = message;
            remaining = displayDuration;
        }

        private void Update()
        {
            if (heart != null && heart.Health <= 0) { Clear(); return; }
            if (Time.timeScale <= 0f || (menus != null && menus.State != GameMenuController.MenuState.Playing))
            {
                panel.alpha = 0f;
                return;
            }
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            opacity = Mathf.MoveTowards(opacity, remaining > 0f ? 1f : 0f, Time.deltaTime / fadeDuration);
            panel.alpha = opacity;
        }

        public void Clear()
        {
            remaining = opacity = 0f;
            panel.alpha = 0f;
        }

        private void OnDisable() { if (panel != null) Clear(); }
    }
}
