using System;
using UnityEngine;

namespace MysticWoods.Gameplay.Enemies
{
    public enum EnemyAttackKind
    {
        /// <summary>Stops, telegraphs a sector and strikes it once.</summary>
        Melee,
        /// <summary>Telegraphs a lane, then dashes along it hitting whatever it touches.</summary>
        Lunge,
        /// <summary>Leaps onto a locked position and damages a circle on landing.</summary>
        Slam,
        /// <summary>Fires a spread of projectiles at the target.</summary>
        Volley,
        /// <summary>Calls reinforcements (boss only).</summary>
        Summon
    }

    [Serializable]
    public sealed class EnemyAttack
    {
        public string name = "Strike";
        public EnemyAttackKind kind = EnemyAttackKind.Melee;
        [Tooltip("Boss phases (1-based) allowed to use this attack. 0 = from phase 1.")]
        [Min(0)] public int minPhase;
        [Min(0f)] public float weight = 1f;
        [Header("Range from the target's body, world units")]
        [Min(0f)] public float minRange;
        [Min(.1f)] public float maxRange = .6f;
        [Header("Timing (seconds)")]
        [Min(.05f)] public float windup = .5f;
        [Min(.02f)] public float active = .16f;
        [Min(.05f)] public float recovery = .4f;
        [Min(0f)] public float cooldown = 1f;
        [Header("Shape")]
        [Tooltip("Melee: reach beyond the body. Lunge: hit radius. Slam: impact radius. Volley: projectile radius.")]
        [Min(.05f)] public float reach = .45f;
        [Range(20f, 360f)] public float arc = 100f;
        [Tooltip("Lunge dash speed, slam leap distance limit, or projectile speed.")]
        [Min(0f)] public float speed = 7f;
        [Header("Volley / Summon")]
        [Min(1)] public int count = 3;
        [Range(0f, 120f)] public float spread = 40f;
        [Header("Damage")]
        [Min(0f)] public float damage = 1f;
        [Min(0f)] public float knockback = 6f;
        public bool heavy;
        [Tooltip("Hits received while preparing interrupt this attack only if poise breaks.")]
        public bool superArmor;
    }

    [CreateAssetMenu(menuName = "Mystic Woods/Enemies/Enemy Configuration")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [Header("Body")]
        [Min(.1f)] public float maxHealth = 3f;
        [Tooltip("Poise absorbed before a hit staggers. 0 = every hit staggers.")]
        [Min(0f)] public float poise = 0f;
        [Min(0f)] public float staggerDuration = .45f;
        [Min(0f)] public float knockbackResistance = 0f;
        [Min(0f)] public float invulnerabilityAfterHit = .08f;

        [Header("Locomotion (world units / seconds)")]
        [Min(.1f)] public float wanderSpeed = .9f;
        [Min(.1f)] public float chaseSpeed = 2f;
        [Min(.01f)] public float acceleration = 14f;
        [Tooltip("Moves in hops: speed pulses with this period (slimes). 0 = continuous.")]
        [Min(0f)] public float hopPeriod = 0f;
        [Min(0f)] public float wanderRadius = 2.5f;
        [Min(0f)] public float wanderPause = 1.8f;

        [Header("Perception")]
        [Min(.5f)] public float sightRadius = 5f;
        [Tooltip("Once engaged, the target stays tracked up to this distance.")]
        [Min(.5f)] public float pursuitRadius = 8.5f;
        [Min(0f)] public float hearingMultiplier = 1f;
        [Tooltip("Seconds between noticing the player and acting (the '!' moment).")]
        [Min(0f)] public float reactionTime = .45f;
        [Tooltip("How long the last seen position is remembered after losing sight.")]
        [Min(0f)] public float memory = 3f;
        [Tooltip("Allies within this radius join the fight when this enemy notices the player.")]
        [Min(0f)] public float alertAllies = 4.5f;
        [Tooltip("Beyond this distance from home the enemy gives up and returns.")]
        [Min(1f)] public float leashRadius = 12f;

        [Header("Tactics")]
        [Tooltip("Preferred distance while waiting for an attack opening.")]
        [Min(.2f)] public float engageDistance = 1.8f;
        [Range(0f, 1f)] public float strafeAmount = .7f;
        [Tooltip("Chance of stepping back after being hit (keeps fights dynamic).")]
        [Range(0f, 1f)] public float retreatOnHit = 0f;
        [Min(0f)] public float retreatTime = .45f;
        [Tooltip("Bosses ignore the shared attack slots.")]
        public bool ignoresAttackSlots;
        [Min(0f)] public float separation = .7f;

        [Header("Death")]
        [Tooltip("Times this enemy rises again after dying (skeletons).")]
        [Min(0)] public int revives;
        [Min(0f)] public float reviveDelay = 2.5f;
        [Range(.1f, 1f)] public float reviveHealth = .5f;
        public Color hitFlash = new Color(1f, 1f, 1f, .95f);
        public Color deathTint = new Color(.55f, 1f, .55f, 1f);

        [Header("Attacks")]
        public EnemyAttack[] attacks = { new EnemyAttack() };

        [Header("Boss")]
        [Tooltip("Health fractions that start each new phase (descending).")]
        public float[] phaseThresholds = new float[0];
        [Min(0f)] public float phaseTransitionTime = 1.4f;
        [Tooltip("Speed and cooldown multipliers applied per phase after the first.")]
        [Min(1f)] public float phaseSpeedUp = 1.18f;
        public GameObject summonPrefab;
        [Min(0)] public int maxSummons = 3;
        public EnemyProjectile projectilePrefab;
        [Tooltip("Radius of the damage-free push released when a new phase starts.")]
        [Min(0f)] public float phaseShockwave = 3.2f;

        public float MaxAttackRange
        {
            get
            {
                float best = 0f;
                if (attacks != null) foreach (var attack in attacks) if (attack != null) best = Mathf.Max(best, attack.maxRange);
                return best;
            }
        }

        private void OnValidate()
        {
            pursuitRadius = Mathf.Max(pursuitRadius, sightRadius);
            if (phaseThresholds != null) Array.Sort(phaseThresholds, (a, b) => b.CompareTo(a));
        }
    }
}
