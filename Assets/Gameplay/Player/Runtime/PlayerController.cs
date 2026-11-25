using System;
using MysticWoods.Gameplay.Combat;
using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    /// <summary>
    /// Action arbiter. Priority: Dead > Hurt > Dodge > Attack > Locomotion.
    /// Reads commands from <see cref="PlayerInputReader"/>, drives <see cref="PlayerMovement"/> and
    /// <see cref="PlayerCombat"/>, and reacts to <see cref="Health"/>. Never writes the Animator.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMovement))]
    public sealed class PlayerController : MonoBehaviour, IPushable
    {
        private PlayerInputReader input;
        private PlayerMovement movement;
        private PlayerCombat combat;
        private Health health;
        private SpriteFlash flash;
        private AfterImage afterImage;
        private PlayerFacing facing = PlayerFacing.Down;
        private int nextSwing;
        private float lastSwingEndedAt = -10f;
        private float actionStartedAt;
        private float dodgeReadyAt;
        private Vector2 dodgeDirection;
        private bool dodgeInvulnerable;

        public event Action<PlayerMotionState> ActionChanged;
        public event Action Died;

        public PlayerMotionState Action { get; private set; } = PlayerMotionState.Idle;
        /// <summary>Facing chosen by an action (attack/dodge); presentation keeps it while the action runs.</summary>
        public PlayerFacing ActionFacing => facing;
        public bool IsActing => Action == PlayerMotionState.Attack || Action == PlayerMotionState.Dodge ||
            Action == PlayerMotionState.Hurt || Action == PlayerMotionState.Dead;
        public bool IsAttacking => Action == PlayerMotionState.Attack;
        public bool IsDead => Action == PlayerMotionState.Dead;
        public float ActionElapsed => Time.time - actionStartedAt;
        public PlayerCombat Combat => combat;
        public Health Health => health;
        public PlayerInputReader Input => input;
        public PlayerMovement Movement => movement;

        private PlayerConfig Config => movement.Config;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            movement = GetComponent<PlayerMovement>();
            combat = GetComponent<PlayerCombat>();
            health = GetComponent<Health>();
            flash = GetComponentInChildren<SpriteFlash>();
            afterImage = GetComponentInChildren<AfterImage>();
            if (health != null && movement.Config != null)
                health.Configure(Team.Player, Config.MaxHealth, 0f, Config.HurtInvulnerability);
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
            EndDodgeInvulnerability();
            if (combat != null) combat.End();
            if (movement != null) movement.Stop();
        }

        private void Update()
        {
            if (Action == PlayerMotionState.Dead) { movement.SetCommand(Vector2.zero, false); return; }
            movement.SetCommand(input.Move, input.Run);
        }

        private void FixedUpdate()
        {
            if (movement.Config == null) return;
            switch (Action)
            {
                case PlayerMotionState.Dead:
                    movement.Override(Vector2.zero);
                    return;
                case PlayerMotionState.Hurt:
                    if (ActionElapsed >= Config.HurtStun) SetAction(PlayerMotionState.Idle);
                    else return;
                    break;
                case PlayerMotionState.Dodge:
                    TickDodge();
                    return;
                case PlayerMotionState.Attack:
                    TickAttack();
                    return;
            }
            // Locomotion: actions start from here.
            if (TryDodge()) return;
            if (combat != null && input.ConsumeAttack(Config.InputBuffer))
            {
                int index = Time.time - lastSwingEndedAt <= Config.ComboGrace ? nextSwing : 0;
                StartSwing(index);
                return;
            }
            movement.SetSpeedScale(1f);
            Vector2 velocity = movement.Velocity;
            if (velocity.sqrMagnitude > Config.MovingThreshold * Config.MovingThreshold)
                facing = PlayerDirection.Resolve(velocity, facing, Config.DirectionHysteresis);
            var next = movement.Velocity.sqrMagnitude > Config.MovingThreshold * Config.MovingThreshold
                ? PlayerMotionState.Walk : PlayerMotionState.Idle;
            if (next != Action) SetAction(next);
        }

        private void StartSwing(int index)
        {
            // Aim with the direction held at the press (or now); otherwise keep the current facing.
            Vector2 aim = input.AttackAim.sqrMagnitude > .04f ? input.AttackAim : input.Move;
            if (aim.sqrMagnitude > .04f) facing = PlayerDirection.Resolve(aim, facing, 0f);
            combat.Begin(index % Config.ComboLength, facing);
            nextSwing = (index + 1) % Config.ComboLength;
            SetAction(PlayerMotionState.Attack);
        }

        private void TickAttack()
        {
            movement.SetSpeedScale(Config.MoveWhileAttacking);
            Vector2 lunge = combat.LungeVelocity();
            if (lunge != Vector2.zero) movement.Override(lunge + input.Move * Config.WalkSpeed * Config.MoveWhileAttacking);
            combat.Step();
            // Dodge cancels the tail of a swing, never its active part.
            if (combat.Progress >= Config.DodgeCancelsSwingAfter && !combat.HitWindowOpen && TryDodge()) return;
            bool chained = combat.CanChain && nextSwing != 0 && input.AttackBuffered(Config.InputBuffer);
            if (chained && combat.Elapsed >= combat.Config.Swing(combat.SwingIndex).hitEnd)
            {
                input.ConsumeAttack(Config.InputBuffer);
                StartSwing(nextSwing);
                return;
            }
            if (!combat.Finished) return;
            combat.End();
            lastSwingEndedAt = Time.time;
            if (nextSwing == 0) lastSwingEndedAt = -10f; // finisher resets the chain
            SetAction(PlayerMotionState.Idle);
        }

        private bool TryDodge()
        {
            if (Time.time < dodgeReadyAt || !input.ConsumeDodge(Config.InputBuffer)) return false;
            Vector2 direction = input.DodgeAim.sqrMagnitude > .04f ? input.DodgeAim : input.Move;
            dodgeDirection = direction.sqrMagnitude > .04f ? direction.normalized : PlayerDirection.ToVector(facing);
            facing = PlayerDirection.Resolve(dodgeDirection, facing, Config.DirectionHysteresis);
            if (combat != null) combat.End();
            nextSwing = 0;
            SetAction(PlayerMotionState.Dodge);
            if (afterImage != null) afterImage.Emit(Config.DodgeDuration);
            CombatVfx.Play(VfxKind.Dust, transform.position, -dodgeDirection, new Color(.75f, .7f, .55f, .8f), 5);
            return true;
        }

        private void TickDodge()
        {
            float t = ActionElapsed;
            bool invulnerable = t >= Config.DodgeInvulnerableFrom && t < Config.DodgeInvulnerableTo;
            if (invulnerable && !dodgeInvulnerable) { health?.PushInvulnerability(); dodgeInvulnerable = true; }
            else if (!invulnerable) EndDodgeInvulnerability();
            // Ease-out burst: fast start, controllable stop.
            float k = Mathf.Clamp01(t / Config.DodgeDuration);
            movement.Override(dodgeDirection * Config.DodgeSpeed * Mathf.Lerp(1.45f, .35f, k));
            if (t < Config.DodgeDuration) return;
            EndDodgeInvulnerability();
            dodgeReadyAt = Time.time + Config.DodgeCooldown;
            SetAction(PlayerMotionState.Idle);
        }

        private void EndDodgeInvulnerability()
        {
            if (!dodgeInvulnerable) return;
            dodgeInvulnerable = false;
            health?.PopInvulnerability();
        }

        private void OnDamaged(DamageInfo info, HitResult result)
        {
            if (result == HitResult.Killed) return;
            if (combat != null) combat.End();
            EndDodgeInvulnerability();
            nextSwing = 0;
            Vector2 push = info.Direction * Mathf.Min(info.Knockback, Config.MaxKnockback);
            movement.Knockback(push, Mathf.Max(.12f, Config.HurtStun));
            SetAction(PlayerMotionState.Hurt);
            if (flash != null)
            {
                flash.Flash(new Color(1f, .25f, .2f, .9f), .16f);
                flash.Blink(Config.HurtInvulnerability);
            }
            HitStop.Request(info.Heavy ? .09f : .06f);
            CameraShake.Add(info.Heavy ? .45f : .28f);
            DamageVignette.Pulse(.9f);
            DamageVignette.SetDanger(health.Current <= 1f ? 1f : health.Current <= 2f ? .5f : 0f);
        }

        private void OnDied(DamageInfo info)
        {
            if (combat != null) combat.End();
            EndDodgeInvulnerability();
            movement.Stop();
            input.ClearBuffered();
            if (flash != null) { flash.StopBlink(); flash.Flash(Color.white, .2f); }
            CameraShake.Add(.5f);
            DamageVignette.Pulse(1f);
            SetAction(PlayerMotionState.Dead);
            Died?.Invoke();
        }

        private void SetAction(PlayerMotionState next)
        {
            if (Action == PlayerMotionState.Dead && next != PlayerMotionState.Dead) return;
            bool changed = Action != next;
            Action = next;
            actionStartedAt = Time.time;
            if (changed || next == PlayerMotionState.Attack) ActionChanged?.Invoke(next);
        }

        public void Push(Vector2 velocity, float duration)
        {
            if (IsDead) return;
            if (combat != null) combat.End();
            EndDodgeInvulnerability();
            movement.Knockback(velocity, duration);
            SetAction(PlayerMotionState.Hurt);
        }

        /// <summary>Called by menus when they close, so the confirming press does not swing.</summary>
        public void SuppressActionsUntilRelease() => input.SuppressActionsUntilRelease();

        /// <summary>Clears every action and stops in place (death screen, scene transitions).</summary>
        public void CancelActions()
        {
            if (combat != null) combat.End();
            EndDodgeInvulnerability();
            input.ClearBuffered();
            movement.Stop();
            if (Action != PlayerMotionState.Dead) SetAction(PlayerMotionState.Idle);
        }
    }
}
