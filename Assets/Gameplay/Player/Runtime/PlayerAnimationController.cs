using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    /// <summary>
    /// Only presentation: reads the physical velocity and the controller's action, and owns every
    /// Animator/flip write. Explicit states per direction, no blend trees between discrete sprites.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        public const string PlaybackRateParameter = "PlaybackRate";
        /// <summary>Length of the authored attack clips (4 frames at 8 fps).</summary>
        public const float AttackClipLength = .5f;
        private static readonly int PlaybackRateId = Animator.StringToHash(PlaybackRateParameter);
        private static readonly int StretchId = Shader.PropertyToID("_IdleStretch");
        private static readonly int[] IdleStates = StateHashes("Idle");
        private static readonly int[] WalkStates = StateHashes("Walk");
        private static readonly int[] AttackStates = StateHashes("Attack");
        private static readonly int DeathState = Animator.StringToHash("Base Layer.Death");
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerController controller;
        [SerializeField] private SpriteRenderer sprite;
        [Tooltip("Walk cycle speed multiplier while dodging (there is no dedicated roll art).")]
        [SerializeField, Min(1f)] private float dodgeCadence = 2.4f;
        private Animator animator;
        private MaterialPropertyBlock properties;
        private int currentHash;
        private int lastSwing = -1;
        private float lastSwingStart = -1f;
        private float breathingPhase;
        private float breathingWeight;
        public PlayerMotionState State { get; private set; }
        public PlayerFacing Facing { get; private set; } = PlayerFacing.Down;
        public int StateChanges { get; private set; }
        public Animator Animator => animator;
        public SpriteRenderer Sprite => sprite;
        public int CurrentStateHash => currentHash;

        public static int AttackStateHash(PlayerFacing facing) => AttackStates[(int)facing];
        public static int DeathStateHash => DeathState;

        private static int[] StateHashes(string name)
        {
            var hashes = new int[4];
            for (int i = 0; i < hashes.Length; i++)
                hashes[i] = Animator.StringToHash("Base Layer." + name + "_" + (PlayerFacing)i);
            return hashes;
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            properties = new MaterialPropertyBlock();
            if (controller == null) controller = GetComponentInParent<PlayerController>();
            if (movement == null || movement.Config == null || sprite == null)
            {
                Debug.LogError("PlayerAnimationController requires movement, configuration and sprite references.", this);
                enabled = false;
            }
        }

        private void OnEnable() { currentHash = 0; lastSwing = -1; }

        private void LateUpdate()
        {
            if (Time.timeScale <= 0f) return;
            var config = movement.Config;
            Vector2 velocity = movement.Velocity;
            var action = controller != null ? controller.Action : PlayerMotionState.Idle;
            float threshold = config.MovingThreshold * (State == PlayerMotionState.Walk ? .5f : 1f);
            bool moving = velocity.sqrMagnitude > threshold * threshold;
            PlayerMotionState next;
            int hash;
            float rate = 1f;
            bool restart = false;
            switch (action)
            {
                case PlayerMotionState.Dead:
                    next = PlayerMotionState.Dead;
                    hash = DeathState;
                    break;
                case PlayerMotionState.Attack:
                    next = PlayerMotionState.Attack;
                    Facing = controller.Combat.Direction;
                    hash = AttackStates[(int)Facing];
                    rate = AttackClipLength / Mathf.Max(.05f, controller.Combat.Duration);
                    // Every swing of the combo restarts the clip, even in the same direction.
                    restart = controller.Combat.SwingIndex != lastSwing || controller.ActionElapsed < lastSwingStart;
                    lastSwing = controller.Combat.SwingIndex;
                    lastSwingStart = controller.ActionElapsed;
                    break;
                case PlayerMotionState.Dodge:
                    next = PlayerMotionState.Dodge;
                    Facing = controller.ActionFacing;
                    hash = WalkStates[(int)Facing];
                    rate = dodgeCadence;
                    break;
                case PlayerMotionState.Hurt:
                    next = PlayerMotionState.Hurt;
                    hash = IdleStates[(int)Facing];
                    break;
                default:
                    next = moving ? PlayerMotionState.Walk : PlayerMotionState.Idle;
                    if (moving) Facing = PlayerDirection.Resolve(velocity, Facing, config.DirectionHysteresis);
                    hash = (moving ? WalkStates : IdleStates)[(int)Facing];
                    rate = moving ? Mathf.Clamp(velocity.magnitude / config.WalkSpeed, .25f, 1.5f) : 1f;
                    break;
            }
            if (action != PlayerMotionState.Attack) lastSwing = -1;
            animator.SetFloat(PlaybackRateId, rate);
            if (hash != currentHash || restart)
            {
                bool keepPhase = State == PlayerMotionState.Walk && next == PlayerMotionState.Walk && currentHash != 0 && !restart;
                float phase = keepPhase ? Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) : 0f;
                animator.Play(hash, 0, phase);
                animator.Update(0f);
                currentHash = hash;
                StateChanges++;
            }
            State = next;
            sprite.flipX = Facing == PlayerFacing.Left;
            bool breathing = next == PlayerMotionState.Idle;
            breathingWeight = Mathf.MoveTowards(breathingWeight, breathing ? 1f : 0f, Time.deltaTime / .12f);
            breathingPhase = Mathf.Repeat(breathingPhase + Time.deltaTime / config.BreathingPeriod, 1f);
            float breath = (.5f - .5f * Mathf.Cos(breathingPhase * Mathf.PI * 2f)) * breathingWeight;
            float squash = next == PlayerMotionState.Dodge ? -.06f : 0f;
            sprite.GetPropertyBlock(properties);
            // The clip aligns the feet at Visual's origin; the shader deforms around that same point.
            properties.SetVector(StretchId, new Vector4(-squash * .6f, config.IdleBreathing * breath + squash,
                -sprite.transform.localPosition.y, sprite.flipX ? sprite.transform.localPosition.x : -sprite.transform.localPosition.x));
            sprite.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            currentHash = 0;
            if (sprite == null || properties == null) return;
            sprite.GetPropertyBlock(properties);
            properties.SetVector(StretchId, Vector4.zero);
            sprite.SetPropertyBlock(properties);
        }
    }
}
