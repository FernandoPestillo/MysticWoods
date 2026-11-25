using MysticWoods.Gameplay.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace MysticWoods.UI
{
    using PlayerController = MysticWoods.Gameplay.Player.PlayerController;

    /// <summary>Draws the player's health as hearts. Reads <see cref="Health"/>; owns no rules.</summary>
    public sealed class HeartsHud : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Image[] hearts = new Image[0];
        [SerializeField] private Sprite full;
        [SerializeField] private Sprite empty;
        [SerializeField, Min(.05f)] private float popDuration = .35f;
        private Vector3[] baseScales;
        private float[] poppedAt;
        private int shown = -1;

        public Health Health => health;

        private void Awake()
        {
            if (health == null)
            {
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null) health = player.GetComponent<Health>();
            }
            baseScales = new Vector3[hearts.Length];
            poppedAt = new float[hearts.Length];
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] != null) baseScales[i] = hearts[i].rectTransform.localScale;
                poppedAt[i] = -10f;
            }
        }

        public void Bind(Health target, Image[] images, Sprite fullSprite, Sprite emptySprite)
        {
            health = target; hearts = images; full = fullSprite; empty = emptySprite;
        }

        private void Update()
        {
            if (health == null) return;
            int current = Mathf.CeilToInt(health.Current - .001f);
            int max = Mathf.CeilToInt(health.Max - .001f);
            if (shown >= 0 && current < shown)
                for (int i = current; i < shown && i < hearts.Length; i++) poppedAt[i] = Time.unscaledTime;
            shown = current;
            for (int i = 0; i < hearts.Length; i++)
            {
                var heart = hearts[i];
                if (heart == null) continue;
                heart.enabled = i < max;
                heart.sprite = i < current ? full : empty;
                // A lost heart jumps and settles; the last heart beats while in danger.
                float age = (Time.unscaledTime - poppedAt[i]) / popDuration;
                float scale = age < 1f ? 1f + .45f * Mathf.Sin(age * Mathf.PI) * (1f - age) : 1f;
                if (current == 1 && i == 0) scale *= 1f + .08f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 6f));
                heart.rectTransform.localScale = baseScales[i] * scale;
            }
        }
    }
}
