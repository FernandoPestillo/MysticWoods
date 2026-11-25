using System;
using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    /// <summary>One swing of the sword combo. Times are seconds from the start of the swing.</summary>
    [Serializable]
    public sealed class PlayerSwing
    {
        [Min(.1f)] public float duration = .36f;
        [Min(0f)] public float hitStart = .07f;
        [Min(0f)] public float hitEnd = .2f;
        [Tooltip("Next swing can be chained from this time on (input pressed earlier is buffered).")]
        [Min(0f)] public float chainFrom = .2f;
        [Min(0f)] public float damage = 1f;
        [Min(0f)] public float poise = 1f;
        [Tooltip("Initial knockback speed applied to enemies, world units per second.")]
        [Min(0f)] public float knockback = 6f;
        [Tooltip("Forward step during the windup, world units per second.")]
        [Min(0f)] public float lunge = 3.2f;
        [Min(.1f)] public float reach = 1.25f;
        [Min(.1f)] public float width = 1.3f;
        [Min(0f)] public float hitStop = .05f;
        [Range(0f, 1f)] public float shake = .12f;
        public bool heavy;
    }

    [CreateAssetMenu(menuName = "Mystic Woods/Player/Configuration")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Movement (world units / seconds)")]
        [SerializeField, Min(.01f)] private float walkSpeed = 3.5f;
        [SerializeField, Min(.01f)] private float runSpeed = 5.25f;
        [SerializeField, Min(.01f)] private float accelerationTime = .12f;
        [SerializeField, Min(.01f)] private float brakingTime = .08f;
        [Header("Presentation")]
        [SerializeField, Min(.001f)] private float movingThreshold = .03f;
        [SerializeField, Range(0f, .1f)] private float directionHysteresis = .03f;
        [SerializeField, Range(0f, .03f)] private float idleBreathing = .015f;
        [SerializeField, Min(.1f)] private float breathingPeriod = 1.8f;

        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 6f;
        [SerializeField, Min(0f)] private float hurtInvulnerability = .9f;
        [SerializeField, Min(0f)] private float hurtStun = .22f;
        [SerializeField, Min(0f)] private float maxKnockback = 9f;

        [Header("Sword combo")]
        [SerializeField] private PlayerSwing[] combo =
        {
            new PlayerSwing(),
            new PlayerSwing { duration = .34f, hitStart = .06f, hitEnd = .19f, chainFrom = .18f, lunge = 3.6f },
            new PlayerSwing { duration = .52f, hitStart = .12f, hitEnd = .28f, chainFrom = .52f, damage = 2f, poise = 3f,
                knockback = 11f, lunge = 6f, reach = 1.45f, width = 1.55f, hitStop = .09f, shake = .3f, heavy = true }
        };
        [Tooltip("Seconds after a swing ends during which the next press still continues the combo.")]
        [SerializeField, Min(0f)] private float comboGrace = .3f;
        [SerializeField, Min(0f)] private float inputBuffer = .2f;
        [SerializeField, Range(0f, 1f)] private float moveWhileAttacking = .2f;
        [SerializeField, Min(0f)] private float swingNoiseRadius = 6f;

        [Header("Dodge")]
        [SerializeField, Min(.05f)] private float dodgeDuration = .3f;
        [SerializeField, Min(.1f)] private float dodgeDistance = 2.6f;
        [SerializeField, Min(0f)] private float dodgeInvulnerableFrom = .02f;
        [SerializeField, Min(0f)] private float dodgeInvulnerableTo = .26f;
        [SerializeField, Min(0f)] private float dodgeCooldown = .38f;
        [Tooltip("A dodge may cancel the recovery of a swing once this fraction of it has passed.")]
        [SerializeField, Range(0f, 1f)] private float dodgeCancelsSwingAfter = .55f;

        public float WalkSpeed => Mathf.Max(.01f, walkSpeed);
        public float RunSpeed => Mathf.Max(WalkSpeed, runSpeed);
        public float AccelerationTime => Mathf.Max(.01f, accelerationTime);
        public float BrakingTime => Mathf.Max(.01f, brakingTime);
        public float MovingThreshold => Mathf.Max(.001f, movingThreshold);
        public float DirectionHysteresis => directionHysteresis;
        public float IdleBreathing => idleBreathing;
        public float BreathingPeriod => Mathf.Max(.1f, breathingPeriod);

        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float HurtInvulnerability => Mathf.Max(0f, hurtInvulnerability);
        public float HurtStun => Mathf.Max(0f, hurtStun);
        public float MaxKnockback => Mathf.Max(0f, maxKnockback);

        public int ComboLength => combo != null ? combo.Length : 0;
        public PlayerSwing Swing(int index) => combo[Mathf.Clamp(index, 0, combo.Length - 1)];
        public float ComboGrace => Mathf.Max(0f, comboGrace);
        public float InputBuffer => Mathf.Max(0f, inputBuffer);
        public float MoveWhileAttacking => moveWhileAttacking;
        public float SwingNoiseRadius => swingNoiseRadius;

        public float DodgeDuration => Mathf.Max(.05f, dodgeDuration);
        public float DodgeSpeed => dodgeDistance / DodgeDuration;
        public float DodgeInvulnerableFrom => dodgeInvulnerableFrom;
        public float DodgeInvulnerableTo => Mathf.Min(dodgeInvulnerableTo, DodgeDuration);
        public float DodgeCooldown => dodgeCooldown;
        public float DodgeCancelsSwingAfter => dodgeCancelsSwingAfter;

        private void OnValidate()
        {
            if (combo == null) return;
            foreach (var swing in combo)
            {
                if (swing == null) continue;
                swing.duration = Mathf.Max(.1f, swing.duration);
                swing.hitStart = Mathf.Clamp(swing.hitStart, 0f, swing.duration - .02f);
                swing.hitEnd = Mathf.Clamp(swing.hitEnd, swing.hitStart + .02f, swing.duration);
                swing.chainFrom = Mathf.Clamp(swing.chainFrom, 0f, swing.duration);
            }
        }
    }
}
