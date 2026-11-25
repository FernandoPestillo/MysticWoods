using MysticWoods.Gameplay.Combat;
using UnityEngine;

namespace MysticWoods.Gameplay.Enemies
{
    /// <summary>
    /// Everything the player sees of an enemy: Animator parameters, facing, breathing, windup
    /// squash, hops and leaps (drawn offsets only), alert icons, ground shadow and dissolve.
    /// It reads the brain; it never changes gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class EnemyPresentation : MonoBehaviour
    {
        private static readonly int IsMoving = Animator.StringToHash("isMoving");
        private static readonly int IsAlive = Animator.StringToHash("isAlive");
        private static readonly int IsAttacking = Animator.StringToHash("isAttacking");
        private static readonly int HitTrigger = Animator.StringToHash("hit");
        private static readonly int LocomotionRate = Animator.StringToHash("LocomotionRate");
        private static readonly int MeleeProgress = Animator.StringToHash("MeleeProgress");
        private static readonly int CanRevive = Animator.StringToHash("canRevive");
        private static readonly int StretchId = Shader.PropertyToID("_IdleStretch");
        private static readonly int AttackStretchId = Shader.PropertyToID("_AttackStretch");
        private static readonly int AttackTintId = Shader.PropertyToID("_AttackTint");
        private static readonly int OffsetId = Shader.PropertyToID("_VisualOffset");

        [SerializeField] private SpriteRenderer sprite;
        [SerializeField] private SpriteRenderer shadow;
        [SerializeField] private SpriteRenderer alertIcon;
        [SerializeField] private Sprite alertSprite;
        [SerializeField] private Sprite searchSprite;
        [SerializeField, Range(0f, .15f)] private float breathing = .025f;
        [SerializeField, Range(0f, .25f)] private float windupSquash = .1f;
        [SerializeField, Min(0f)] private float hopHeight = .16f;

        private EnemyBrain brain;
        private Animator animator;
        private SpriteFlash flash;
        private MaterialPropertyBlock block;
        private bool hasCanRevive;
        private float breathPhase, breathWeight;
        private float iconShownAt = -10f, iconDuration;
        private Vector3 shadowScale;
        private float lift;

        public bool AlertVisible => alertIcon != null && alertIcon.enabled;
        public SpriteRenderer Sprite => sprite;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            animator = GetComponent<Animator>();
            flash = GetComponent<SpriteFlash>();
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();
            block = new MaterialPropertyBlock();
            if (shadow != null) shadowScale = shadow.transform.localScale;
            if (alertIcon != null) alertIcon.enabled = false;
            breathPhase = Mathf.Repeat(transform.position.x * .73f + transform.position.y * .37f, Mathf.PI * 2f);
            if (animator != null)
                foreach (var parameter in animator.parameters)
                    if (parameter.nameHash == CanRevive) hasCanRevive = true;
        }

        private void Start() { if (brain != null && brain.Config != null) SetAlive(brain.IsAlive, brain.Config.revives > 0); }

        public void Bind(SpriteRenderer body, SpriteRenderer groundShadow, SpriteRenderer icon, Sprite alert, Sprite search)
        {
            sprite = body; shadow = groundShadow; alertIcon = icon; alertSprite = alert; searchSprite = search;
        }

        public void ShowAlert() => ShowIcon(alertSprite, .7f);
        public void ShowSearch() => ShowIcon(searchSprite, 1.1f);

        private void ShowIcon(Sprite icon, float duration)
        {
            if (alertIcon == null || icon == null) return;
            alertIcon.sprite = icon;
            iconShownAt = Time.time;
            iconDuration = duration;
        }

        public void Hit(Color color)
        {
            if (animator != null && brain.IsAlive) animator.SetTrigger(HitTrigger);
            if (flash != null) flash.Flash(color, .11f);
        }

        public void SetAlive(bool alive, bool willRevive)
        {
            if (animator == null) return;
            if (hasCanRevive) animator.SetBool(CanRevive, willRevive);
            animator.SetBool(IsAlive, alive);
            if (!alive) { animator.SetBool(IsAttacking, false); animator.SetBool(IsMoving, false); }
        }

        public void SetFade(float amount) { if (flash != null) flash.SetFade(amount); }

        private void LateUpdate()
        {
            if (sprite == null || brain == null || Time.timeScale <= 0f) return;
            var state = brain.State;
            Vector2 velocity = brain.Motor.Velocity;
            bool alive = brain.IsAlive;
            if (animator != null && animator.isActiveAndEnabled)
            {
                bool attacking = state == EnemyState.Attack;
                animator.SetBool(IsAttacking, attacking && brain.AttackPhase != AttackStage.Recovery);
                animator.SetBool(IsMoving, alive && !attacking && velocity.sqrMagnitude > .04f);
                animator.SetFloat(LocomotionRate, Mathf.Clamp(velocity.magnitude / Mathf.Max(.1f, brain.Config.chaseSpeed), .5f, 1.4f));
                animator.SetFloat(MeleeProgress, attacking ? brain.AttackProgress : 0f);
            }
            Vector2 facing = brain.Facing;
            if (Mathf.Abs(facing.x) > .05f) sprite.flipX = facing.x < 0f;

            // Breathing while idle, squash while winding up, lift for hops and leaps.
            bool idle = alive && state != EnemyState.Attack && velocity.sqrMagnitude < .04f;
            breathWeight = Mathf.MoveTowards(breathWeight, idle ? 1f : 0f, Time.deltaTime / .15f);
            breathPhase = Mathf.Repeat(breathPhase + Time.deltaTime * Mathf.PI * 2f / 1.8f, Mathf.PI * 2f);
            float breath = (.5f - .5f * Mathf.Cos(breathPhase)) * breathWeight;
            var bounds = sprite.sprite != null ? sprite.sprite.bounds : new Bounds();
            float squash = 0f;
            Color tint = Color.clear;
            if (state == EnemyState.Attack)
            {
                float p = brain.AttackPhaseProgress;
                if (brain.AttackPhase == AttackStage.Windup) { squash = Mathf.SmoothStep(0f, 1f, p); tint = new Color(1f, .62f, .12f, .1f + p * .25f); }
                else if (brain.AttackPhase == AttackStage.Active) { squash = -.45f; tint = new Color(1f, .25f, .1f, .18f); }
            }
            else if (state == EnemyState.Phase) tint = new Color(.7f, 1f, .5f, .25f + .2f * Mathf.Sin(Time.time * 20f));
            float targetLift = brain.Lift;
            if (brain.Config.hopPeriod > 0f && alive && state != EnemyState.Attack && velocity.sqrMagnitude > .04f)
                targetLift = Mathf.Abs(Mathf.Sin(brain.HopPhase * Mathf.PI)) * hopHeight;
            lift = Mathf.Lerp(lift, targetLift, 1f - Mathf.Exp(-Time.deltaTime * 30f));
            Vector3 scale = transform.lossyScale;
            sprite.GetPropertyBlock(block);
            block.SetVector(StretchId, new Vector4(0f, breathing * breath, bounds.min.y, bounds.center.x));
            block.SetVector(AttackStretchId, new Vector4(windupSquash * .65f * squash, -windupSquash * squash, bounds.min.y, bounds.center.x));
            block.SetColor(AttackTintId, tint);
            block.SetVector(OffsetId, new Vector4(0f, lift / Mathf.Max(.01f, Mathf.Abs(scale.y)), 0f, 0f));
            sprite.SetPropertyBlock(block);

            if (shadow != null)
            {
                float k = 1f - Mathf.Clamp01(lift / .9f) * .45f;
                shadow.transform.localScale = shadowScale * k;
                shadow.enabled = sprite.enabled && (alive || brain.State == EnemyState.Downed);
                var c = shadow.color; c.a = .45f * k * (1f - (flash != null ? flash.Fade : 0f)); shadow.color = c;
            }
            UpdateIcon();
        }

        private void UpdateIcon()
        {
            if (alertIcon == null) return;
            float age = Time.time - iconShownAt;
            bool visible = age < iconDuration && brain.IsAlive;
            alertIcon.enabled = visible;
            if (!visible) return;
            // Pop in, hold, then shrink away.
            float pop = age < .12f ? Mathf.SmoothStep(.4f, 1.15f, age / .12f) : Mathf.Lerp(1.15f, 1f, (age - .12f) / .1f);
            if (age > iconDuration - .15f) pop *= Mathf.Clamp01((iconDuration - age) / .15f);
            alertIcon.transform.localScale = Vector3.one * pop / Mathf.Max(.01f, Mathf.Abs(transform.lossyScale.x));
        }
    }
}
