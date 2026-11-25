using MysticWoods.Gameplay.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MysticWoods.UI
{
    /// <summary>
    /// Top-of-screen bar for the boss. Appears when the boss engages, shows a delayed "damage
    /// trail", marks phase thresholds and fades out after the boss falls.
    /// </summary>
    public sealed class BossHealthBar : MonoBehaviour
    {
        [SerializeField] private EnemyBrain boss;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image fill;
        [SerializeField] private Image trail;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string bossName = "REI SLIME";
        [SerializeField, Min(.05f)] private float fade = .35f;
        private float trailValue = 1f;
        private float trailHoldUntil;
        private float lastValue = 1f;
        private bool defeated;
        private float defeatedAt;

        public bool Showing => group != null && group.alpha > .5f;

        public void Bind(EnemyBrain target, CanvasGroup canvasGroup, Image fillImage, Image trailImage, TMP_Text text)
        {
            boss = target; group = canvasGroup; fill = fillImage; trail = trailImage; label = text;
        }

        private void Awake()
        {
            if (boss == null)
                foreach (var brain in FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include))
                    if (brain.Config != null && brain.Config.phaseThresholds != null && brain.Config.phaseThresholds.Length > 0) { boss = brain; break; }
            if (group != null) group.alpha = 0f;
            if (label != null) label.text = bossName;
        }

        private void OnEnable() { if (boss != null) boss.Defeated += OnDefeated; }
        private void OnDisable() { if (boss != null) boss.Defeated -= OnDefeated; }
        private void OnDefeated() { defeated = true; defeatedAt = Time.unscaledTime; }

        private void Update()
        {
            if (group == null) return;
            float value = boss != null && boss.Health != null ? boss.Health.Normalized : 0f;
            bool visible = boss != null && boss.Engaged && !defeated;
            if (defeated) visible = Time.unscaledTime - defeatedAt < 1.2f;
            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime / fade);
            if (value < lastValue) trailHoldUntil = Time.unscaledTime + .45f;
            lastValue = value;
            if (Time.unscaledTime > trailHoldUntil) trailValue = Mathf.MoveTowards(trailValue, value, Time.unscaledDeltaTime * .8f);
            trailValue = Mathf.Max(trailValue, value);
            if (fill != null) fill.fillAmount = value;
            if (trail != null) trail.fillAmount = trailValue;
        }
    }
}
