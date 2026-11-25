using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    /// <summary>Only presentation: reads physical velocity and owns all Animator/flip writes.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        public const string PlaybackRateParameter = "PlaybackRate";
        private static readonly int PlaybackRateId = Animator.StringToHash(PlaybackRateParameter);
        private static readonly int StretchId = Shader.PropertyToID("_IdleStretch");
        private static readonly int[] IdleStates = StateHashes("Idle");
        private static readonly int[] WalkStates = StateHashes("Walk");
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private SpriteRenderer sprite;
        private Animator animator;
        private MaterialPropertyBlock properties;
        private int currentHash;
        private float breathingPhase;
        private float breathingWeight;
        public PlayerMotionState State { get; private set; }
        public PlayerFacing Facing { get; private set; } = PlayerFacing.Down;
        public int StateChanges { get; private set; }
        public Animator Animator => animator;
        public SpriteRenderer Sprite => sprite;

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
            if (movement == null || movement.Config == null || sprite == null)
            {
                Debug.LogError("PlayerAnimationController requires movement, configuration and sprite references.", this);
                enabled = false;
            }
        }
        private void OnEnable() => currentHash = 0;
        private void LateUpdate()
        {
            if (Time.timeScale <= 0f) return;
            var config = movement.Config;
            Vector2 velocity = movement.Velocity;
            float threshold = config.MovingThreshold * (State == PlayerMotionState.Walk ? .5f : 1f);
            bool moving = velocity.sqrMagnitude > threshold * threshold;
            var next = moving ? PlayerMotionState.Walk : PlayerMotionState.Idle;
            if (moving) Facing = PlayerDirection.Resolve(velocity, Facing, config.DirectionHysteresis);
            int hash = (moving ? WalkStates : IdleStates)[(int)Facing];
            animator.SetFloat(PlaybackRateId, moving ? Mathf.Clamp(velocity.magnitude / config.WalkSpeed, .25f, 1.5f) : 1f);
            if (hash != currentHash)
            {
                float phase = State == PlayerMotionState.Walk && next == PlayerMotionState.Walk && currentHash != 0
                    ? Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) : 0f;
                animator.Play(hash, 0, phase);
                animator.Update(0f);
                currentHash = hash;
                StateChanges++;
            }
            State = next;
            sprite.flipX = Facing == PlayerFacing.Left;
            breathingWeight = Mathf.MoveTowards(breathingWeight, moving ? 0f : 1f, Time.deltaTime / .12f);
            breathingPhase = Mathf.Repeat(breathingPhase + Time.deltaTime / config.BreathingPeriod, 1f);
            float breath = (.5f - .5f * Mathf.Cos(breathingPhase * Mathf.PI * 2f)) * breathingWeight;
            sprite.GetPropertyBlock(properties);
            // The clip aligns the feet at Visual's origin; the shader breathes around that same point.
            properties.SetVector(StretchId, new Vector4(0, config.IdleBreathing * breath,
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
