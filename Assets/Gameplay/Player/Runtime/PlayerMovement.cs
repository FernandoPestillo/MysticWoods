using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;
        private Rigidbody2D body;
        private Vector2 command;
        private bool running;
        private float brakingSpeed;
        public PlayerConfig Config => config;
        public Vector2 Velocity => body != null && body.simulated ? body.linearVelocity : Vector2.zero;
        public Vector2 Command => command;

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
        private void FixedUpdate()
        {
            if (!body.simulated) return;
            Vector2 target = command * (running ? config.RunSpeed : config.WalkSpeed);
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
            if (body != null) body.linearVelocity = Vector2.zero;
        }
        private void OnDisable() => Stop();
    }
}
