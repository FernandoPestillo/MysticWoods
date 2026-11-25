using System;
using System.Collections.Generic;
using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>
    /// Health, invulnerability and poise rules only. It never moves, animates or draws anything:
    /// controllers and HUDs react to its events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IHittable
    {
        private static readonly List<Health> active = new List<Health>();
        /// <summary>Living, enabled health components; used for targeting without tags or scene searches.</summary>
        public static IReadOnlyList<Health> Active => active;

        [SerializeField] private Team team = Team.Enemy;
        [SerializeField, Min(.1f)] private float maxHealth = 3f;
        [Tooltip("Seconds of invulnerability granted after each damaging hit.")]
        [SerializeField, Min(0f)] private float invulnerabilityAfterHit = .1f;
        [Tooltip("Poise absorbed before the owner is staggered. 0 = every hit staggers.")]
        [SerializeField, Min(0f)] private float maxPoise = 1f;
        [SerializeField, Min(0f)] private float poiseRecoveryDelay = 1.2f;

        private float current;
        private float poise;
        private float invulnerableUntil;
        private float lastHitAt = -100f;
        private int invulnerabilityLocks;
        private bool initialized;

        /// <summary>Raised for every accepted hit, after health was reduced.</summary>
        public event Action<DamageInfo, HitResult> Damaged;
        /// <summary>Raised once when poise breaks; the owner should interrupt its current action.</summary>
        public event Action<DamageInfo> Staggered;
        public event Action<DamageInfo> Died;
        public event Action<float, float> Changed;

        public Team Team { get => team; set => team = value; }
        public float Current { get { Init(); return current; } }
        public float Max => maxHealth;
        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsDead => Current <= 0f;
        public bool IsInvulnerable => invulnerabilityLocks > 0 || Time.time < invulnerableUntil;
        public bool CanBeHit => enabled && gameObject.activeInHierarchy && !IsDead && !IsInvulnerable;
        public float InvulnerabilityAfterHit { get => invulnerabilityAfterHit; set => invulnerabilityAfterHit = Mathf.Max(0f, value); }
        public float MaxPoise { get => maxPoise; set => maxPoise = Mathf.Max(0f, value); }
        public float Poise => poise;
        public float LastHitTime => lastHitAt;
        public DamageInfo LastHit { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();

        private void Awake() => Init();

        private void Init()
        {
            if (initialized) return;
            initialized = true;
            current = maxHealth;
            poise = maxPoise;
        }

        private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        private void OnDisable()
        {
            active.Remove(this);
            invulnerabilityLocks = 0;
        }

        private void Update()
        {
            if (poise < maxPoise && Time.time - lastHitAt > poiseRecoveryDelay) poise = maxPoise;
        }

        public void Configure(Team owner, float max, float poiseCapacity, float invulnerability)
        {
            team = owner;
            maxHealth = Mathf.Max(.1f, max);
            maxPoise = Mathf.Max(0f, poiseCapacity);
            invulnerabilityAfterHit = Mathf.Max(0f, invulnerability);
            initialized = true;
            current = maxHealth;
            poise = maxPoise;
            Changed?.Invoke(current, maxHealth);
        }

        public HitResult ReceiveHit(in DamageInfo info)
        {
            Init();
            if (!CanBeHit || info.Amount <= 0f) return HitResult.Ignored;
            if (info.SourceTeam != Team.Neutral && info.SourceTeam == team) return HitResult.Ignored;
            current = Mathf.Max(0f, current - info.Amount);
            lastHitAt = Time.time;
            LastHit = info;
            invulnerableUntil = Time.time + invulnerabilityAfterHit;
            var result = current <= 0f ? HitResult.Killed : HitResult.Damaged;
            Changed?.Invoke(current, maxHealth);
            Damaged?.Invoke(info, result);
            if (result == HitResult.Killed)
            {
                Died?.Invoke(info);
                return result;
            }
            poise -= info.Poise;
            if (poise <= 0f)
            {
                poise = maxPoise;
                Staggered?.Invoke(info);
            }
            return result;
        }

        public void Heal(float amount)
        {
            Init();
            if (IsDead || amount <= 0f) return;
            current = Mathf.Min(maxHealth, current + amount);
            Changed?.Invoke(current, maxHealth);
        }

        /// <summary>Restores a dead or damaged owner. Used by revives and level restarts.</summary>
        public void Restore(float fraction = 1f)
        {
            Init();
            current = Mathf.Clamp(maxHealth * fraction, .1f, maxHealth);
            poise = maxPoise;
            invulnerableUntil = 0f;
            Changed?.Invoke(current, maxHealth);
        }

        public void Kill(GameObject source = null)
        {
            Init();
            if (IsDead) return;
            var info = new DamageInfo(current, Vector2.zero, 0f, 0f, source, Team.Neutral, transform.position);
            current = 0f;
            LastHit = info;
            Changed?.Invoke(current, maxHealth);
            Damaged?.Invoke(info, HitResult.Killed);
            Died?.Invoke(info);
        }

        /// <summary>Stackable invulnerability (dodges, phase transitions). Every Push needs a Pop.</summary>
        public void PushInvulnerability() => invulnerabilityLocks++;
        public void PopInvulnerability() => invulnerabilityLocks = Mathf.Max(0, invulnerabilityLocks - 1);
        public void GrantInvulnerability(float seconds) => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);

        /// <summary>Closest living opponent of <paramref name="viewer"/>, or null.</summary>
        public static Health ClosestOpponent(Team viewer, Vector2 position, float maxDistance)
        {
            Health best = null;
            float bestDistance = maxDistance * maxDistance;
            foreach (var health in active)
            {
                if (health == null || health.IsDead || health.team == viewer || health.team == Team.Neutral) continue;
                float distance = ((Vector2)health.transform.position - position).sqrMagnitude;
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = health;
            }
            return best;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(.1f, maxHealth);
            maxPoise = Mathf.Max(0f, maxPoise);
        }
    }
}
