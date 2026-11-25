using UnityEngine;
using UnityEngine.Rendering;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>Pulses a dedicated global Volume (red vignette, desaturation) when the player is hurt.</summary>
    [RequireComponent(typeof(Volume))]
    public sealed class DamageVignette : MonoBehaviour
    {
        private static DamageVignette instance;
        [SerializeField, Min(.05f)] private float fadeOut = .45f;
        [SerializeField, Range(0f, 1f)] private float lowHealthPulse = .35f;
        private Volume volume;
        private float pulse;
        private float lowHealth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public static void Pulse(float strength = 1f) { if (instance != null) instance.pulse = Mathf.Max(instance.pulse, strength); }
        /// <summary>0 = healthy, 1 = last heart: a slow heartbeat keeps the danger visible.</summary>
        public static void SetDanger(float amount) { if (instance != null) instance.lowHealth = Mathf.Clamp01(amount); }

        private void Awake()
        {
            volume = GetComponent<Volume>();
            volume.weight = 0f;
        }

        private void OnEnable() => instance = this;
        private void OnDisable() { if (instance == this) instance = null; }

        private void Update()
        {
            pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime / fadeOut);
            float beat = lowHealth > 0f ? lowHealth * lowHealthPulse * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5.5f)) : 0f;
            volume.weight = Mathf.Clamp01(Mathf.Max(pulse, beat));
        }
    }
}
