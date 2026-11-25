using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>
    /// Trauma-based screen shake state. Gameplay calls <see cref="Add"/>; a
    /// <see cref="CinemachineShake"/> extension on the virtual camera turns it into motion.
    /// </summary>
    public static class CameraShake
    {
        private static float trauma;
        private static float lastDecayAt;

        /// <summary>Global intensity multiplier, 0 disables shake.</summary>
        public static float Intensity { get; set; } = 1f;
        public static float DecayPerSecond { get; set; } = 2.6f;
        public static int Requests { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { trauma = 0f; Intensity = 1f; Requests = 0; }

        /// <param name="amount">0..1 trauma; small hits ~.15, heavy ~.4, boss slams ~.7.</param>
        public static void Add(float amount)
        {
            Requests++;
            if (trauma <= 0f) lastDecayAt = Time.unscaledTime;
            trauma = Mathf.Clamp01(trauma + amount * Intensity);
        }

        /// <summary>Current trauma after decay. Unscaled, so it keeps shaking through hit-stop; pause freezes it.</summary>
        public static float Sample()
        {
            float now = Time.unscaledTime;
            if (Time.timeScale > 0f && now > lastDecayAt)
                trauma = Mathf.Max(0f, trauma - DecayPerSecond * Mathf.Min(.1f, now - lastDecayAt));
            lastDecayAt = now;
            return trauma;
        }

        public static void Clear() => trauma = 0f;
    }
}
