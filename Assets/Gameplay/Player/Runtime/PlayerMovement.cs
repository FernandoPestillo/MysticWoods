using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    /// <summary>
    /// Physical motor. Steers toward a commanded velocity, or follows an explicit override
    /// (dodge, sword lunge) or a decaying knockback. Knows nothing about input, animation or health.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;
        private Rigidbody2D body;
        private Vector2 command;
        private bool running;
        private float brakingSpeed;
        private float speedScale = 1f;
        private bool overriding;
        private Vector2 overrideVelocity;
        private Vector2 knockback;
        private float knockbackUntil;
        private float knockbackDuration;
        private float knockbackStart;
        public PlayerConfig Config => config;
        public Vector2 Velocity => body != null && body.simulated ? body.linearVelocity : Vector2.zero;
        public Vector2 Command => command;
        public bool InKnockback => Time.time < knockbackUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (config == null)
            {
                Debug.LogError("PlayerMovement requires a PlayerConfig asset.", this);
                enabled = false;
            }
        }

        public void SetCommand(Vector2 direction, bool run)
        {
            command = Vector2.ClampMagnitude(direction, 1f);
            running = run;
        }

        /// <summary>Fraction of the normal speed while steering (e.g. reduced while swinging).</summary>
        public void SetSpeedScale(float scale) => speedScale = Mathf.Clamp01(scale);

        /// <summary>Drive this exact velocity on the next physics step only.</summary>
        public void Override(Vector2 velocity)
        {
            overriding = true;
            overrideVelocity = velocity;
        }

        /// <summary>Push the body; the push decays to zero over <paramref name="duration"/>.</summary>
        public void Knockback(Vector2 velocity, float duration)
        {
            knockback = velocity;
            knockbackStart = Time.time;
            knockbackDuration = Mathf.Max(.02f, duration);
            knockbackUntil = Time.time + knockbackDuration;
            brakingSpeed = 0;
        }

        private void FixedUpdate()
        {
            if (!body.simulated) return;
            if (InKnockback)
            {
                float t = Mathf.Clamp01((Time.time - knockbackStart) / knockbackDuration);
                body.linearVelocity = knockback * (1f - t) * (1f - t);
                overriding = false;
                return;
            }
            if (overriding)
            {
                body.linearVelocity = overrideVelocity;
                overriding = false;
                brakingSpeed = 0;
                return;
            }
            Vector2 target = command * (running ? config.RunSpeed : config.WalkSpeed) * speedScale;
            Vector2 current = body.linearVelocity;
            bool braking = target.sqrMagnitude < current.sqrMagnitude;
            brakingSpeed = braking ? Mathf.Max(brakingSpeed, current.magnitude) : 0;
            float reference = Mathf.Max(config.WalkSpeed, Mathf.Max(target.magnitude, brakingSpeed));
            float duration = braking ? config.BrakingTime : config.AccelerationTime;
            body.linearVelocity = Vector2.MoveTowards(current, target, reference * Time.fixedDeltaTime / duration);
        }

        public void Stop()
        {
            command = Vector2.zero;
            running = false;
            brakingSpeed = 0;
            overriding = false;
            knockbackUntil = 0;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void OnDisable() => Stop();
    }
}
