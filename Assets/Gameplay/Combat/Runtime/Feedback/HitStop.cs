using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>
    /// Brief global slow-down on impact. Never fights the pause menu: it does not start while
    /// time is frozen and only restores the time scale it set itself.
    /// </summary>
    public sealed class HitStop : MonoBehaviour
    {
        public const float SlowScale = .04f;
        private static HitStop instance;
        private float releaseAt;
        private bool holding;

        /// <summary>Global switch (accessibility / tests).</summary>
        public static bool Enabled { get; set; } = true;
        public static bool Active => instance != null && instance.holding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; Enabled = true; }

        public static void Request(float seconds)
        {
            if (!Enabled || seconds <= 0f || !Application.isPlaying) return;
            if (Time.timeScale <= 0f) return; // paused or game over
            if (instance == null)
            {
                var go = new GameObject("[HitStop]") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HitStop>();
            }
            instance.Hold(seconds);
        }

        /// <summary>Restores normal time immediately if this component froze it.</summary>
        public static void Cancel() { if (instance != null) instance.Release(); }

        private void Hold(float seconds)
        {
            if (!holding && !Mathf.Approximately(Time.timeScale, 1f)) return; // someone else owns time
            releaseAt = Mathf.Max(holding ? releaseAt : 0f, Time.unscaledTime + seconds);
            holding = true;
            Time.timeScale = SlowScale;
        }

        private void Update()
        {
            if (!holding) return;
            // Pause menus set the scale to 0; they now own time and will restore it themselves.
            if (!Mathf.Approximately(Time.timeScale, SlowScale)) { holding = false; return; }
            if (Time.unscaledTime >= releaseAt) Release();
        }

        private void Release()
        {
            if (holding && Mathf.Approximately(Time.timeScale, SlowScale)) Time.timeScale = 1f;
            holding = false;
        }

        private void OnDestroy() { if (instance == this) { Release(); instance = null; } }
    }
}
