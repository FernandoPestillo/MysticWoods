using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>
    /// Hit flash, invulnerability blink and pixel dissolve for sprites drawn with the
    /// "Mystic Woods/Idle Sprite" shader. Shares the renderer's property block with the
    /// breathing/telegraph effects, so it only writes its own properties.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteFlash : MonoBehaviour
    {
        private static readonly int FlashId = Shader.PropertyToID("_FlashColor");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        [SerializeField] private SpriteRenderer target;
        private MaterialPropertyBlock block;
        private Color flashColor;
        private float flashStart, flashEnd;
        private float blinkUntil;
        private float fade;
        private bool dirty;

        public SpriteRenderer Target => target;
        public float Fade => fade;
        public bool Flashing => Time.time < flashEnd;

        private void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            block = new MaterialPropertyBlock();
        }

        public void Bind(SpriteRenderer renderer) => target = renderer;

        public void Flash(Color color, float duration = .12f)
        {
            flashColor = color;
            flashStart = Time.time;
            flashEnd = Time.time + Mathf.Max(.01f, duration);
            dirty = true;
        }

        /// <summary>Alternating visibility, the classic invulnerability read.</summary>
        public void Blink(float duration) { blinkUntil = Mathf.Max(blinkUntil, Time.time + duration); dirty = true; }
        public void StopBlink() { blinkUntil = 0f; dirty = true; }
        public void SetFade(float value) { fade = Mathf.Clamp01(value); dirty = true; }

        private void LateUpdate()
        {
            if (target == null) return;
            bool flashing = Time.time < flashEnd;
            bool blinking = Time.time < blinkUntil;
            if (!flashing && !blinking && !dirty) return;
            dirty = flashing || blinking;
            // Hold the peak briefly, then ease out: a crisp frame of pure colour reads as impact.
            float t = flashing ? Mathf.InverseLerp(flashStart, flashEnd, Time.time) : 1f;
            float alpha = t < .35f ? 1f : 1f - (t - .35f) / .65f;
            target.GetPropertyBlock(block);
            block.SetColor(FlashId, new Color(flashColor.r, flashColor.g, flashColor.b, alpha * alpha * flashColor.a));
            // Blink by dissolving half the frames instead of toggling the renderer (keeps lights/shadows stable).
            float blinkFade = blinking && Mathf.Repeat(Time.time * 14f, 1f) < .5f ? .65f : 0f;
            block.SetFloat(FadeId, Mathf.Max(fade, blinkFade));
            target.SetPropertyBlock(block);
        }

        private void OnDisable()
        {
            if (target == null || block == null) return;
            target.GetPropertyBlock(block);
            block.SetColor(FlashId, Color.clear);
            block.SetFloat(FadeId, fade);
            target.SetPropertyBlock(block);
        }
    }
}
