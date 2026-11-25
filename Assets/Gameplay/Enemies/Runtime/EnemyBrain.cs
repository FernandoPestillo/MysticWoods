using System.Collections.Generic;
using MysticWoods.Gameplay.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace MysticWoods.Gameplay.Enemies
{
    public enum EnemyState { Idle, Wander, Alert, Chase, Engage, Attack, Stagger, Retreat, Search, Return, Phase, Downed, Dead }
    public enum AttackStage { None, Windup, Active, Recovery }

    /// <summary>
    /// Finite-state AI shared by slimes, skeletons and the slime boss. Behaviour differences come
    /// from <see cref="EnemyConfig"/>: perception, tactics, attack list, revives and boss phases.
    /// Runs on the physics step so timings are deterministic and pause/hit-stop aware.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(EnemyMotor))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        private static readonly List<EnemyBrain> brains = new List<EnemyBrain>();
        public static IReadOnlyList<EnemyBrain> All => brains;

        [SerializeField] private EnemyConfig config;
        [SerializeField] private Material telegraphMaterial;
        [Tooltip("Objects switched off when this enemy is defeated for good (boss barriers).")]
        [SerializeField] private GameObject[] deactivateOnDefeat = new GameObject[0];
        [SerializeField] private UnityEvent onDefeated = new UnityEvent();

        private Health health;
        private EnemyMotor motor;
        private EnemyPresentation presentation;
        private EnemyTelegraph telegraph;
        private Rigidbody2D body;
        private readonly HashSet<IHittable> struck = new HashSet<IHittable>();
        private readonly List<GameObject> summons = new List<GameObject>();
        private float[] attackReadyAt;

        private Health target;
        private Collider2D targetCollider;
        private Vector2 home;
        private Vector2 lastSeen;
        private float lastSeenAt = -100f;
        private bool seeing;
        private float nextSenseAt;
        private float stateStartedAt;
        private float stateDuration;
        private Vector2 moveGoal;
        private float strafeSign = 1f;
        private float strafeSwitchAt;
        private float nextAttackAt;
        private int revivesLeft;
        private int phase = 1;
        private float speedScale = 1f;
        private bool phaseInvulnerable;
        private bool retreatPending;
        private float deathAt;

        private EnemyAttack attack;
        private int attackIndex = -1;
        private float attackStageAt;
        private Vector2 attackDirection = Vector2.right;
        private Vector2 attackPoint;
        private Vector2 leapFrom;
        private bool attackFired;

        public EnemyConfig Config => config;
        public EnemyState State { get; private set; } = EnemyState.Idle;
        public AttackStage AttackPhase { get; private set; }
        public EnemyAttack CurrentAttack => attack;
        public EnemyMotor Motor => motor;
        public Health Health => health;
        public Health Target => target;
        public bool Engaged { get; private set; }
        public bool SeesTarget => seeing;
        public bool IsAlive => health != null && !health.IsDead;
        public int BossPhase => phase;
        public Vector2 Facing { get; private set; } = Vector2.right;
        public Vector2 Home => home;
        /// <summary>Visual height of a leap, world units (drawn by the presentation only).</summary>
        public float Lift { get; private set; }
        public float HopPhase { get; private set; }
        public float AttackPhaseProgress
        {
            get
            {
                if (attack == null) return 0f;
                float duration = AttackPhase == AttackStage.Windup ? attack.windup :
                    AttackPhase == AttackStage.Active ? attack.active : attack.recovery;
                return Mathf.Clamp01((Time.time - attackStageAt) / Mathf.Max(.01f, duration));
            }
        }
        /// <summary>0..1 across the whole attack; matches the legacy melee animation timing.</summary>
        public float AttackProgress => AttackPhase == AttackStage.Windup ? .34f * AttackPhaseProgress :
            AttackPhase == AttackStage.Active ? Mathf.Lerp(.5f, .84f, AttackPhaseProgress) :
            AttackPhase == AttackStage.Recovery ? Mathf.Lerp(.84f, 1f, AttackPhaseProgress) : 0f;
        public bool TelegraphVisible => telegraph != null && telegraph.Visible;
        public int AliveSummons { get { summons.RemoveAll(x => x == null); return summons.Count; } }

        public event System.Action<EnemyState> StateChanged;
        public event System.Action<int> PhaseChanged;
        public event System.Action Defeated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => brains.Clear();

        public void Configure(EnemyConfig value, Material telegraphMat)
        {
            config = value;
            telegraphMaterial = telegraphMat;
        }

        /// <summary>Swap the tuning before the enemy wakes up (tests, spawners). Call on an inactive instance.</summary>
        public void UseConfig(EnemyConfig value) => config = value;

        public void SetDefeatTargets(params GameObject[] targets) => deactivateOnDefeat = targets ?? new GameObject[0];

        private void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<EnemyMotor>();
            presentation = GetComponent<EnemyPresentation>();
            body = GetComponent<Rigidbody2D>();
            telegraph = GetComponent<EnemyTelegraph>();
            if (telegraph == null) telegraph = gameObject.AddComponent<EnemyTelegraph>();
            if (config == null)
            {
                Debug.LogError("EnemyBrain requires an EnemyConfig.", this);
                enabled = false;
                return;
            }
            health.Configure(Team.Enemy, config.maxHealth, config.poise, config.invulnerabilityAfterHit);
            motor.Acceleration = config.acceleration;
            motor.Separation = config.separation;
            attackReadyAt = new float[config.attacks.Length];
            revivesLeft = config.revives;
            home = transform.position;
            var sprite = GetComponent<SpriteRenderer>();
            telegraph.Initialise(telegraphMaterial, sprite != null ? sprite.sortingLayerID : 0, sprite != null ? sprite.sortingOrder - 1 : 0);
            nextSenseAt = Time.time + Random.value * .1f;
            strafeSign = Random.value < .5f ? -1f : 1f;
        }

        private void OnEnable()
        {
            brains.Add(this);
            if (health == null) return;
            health.Damaged += OnDamaged;
            health.Staggered += OnStaggered;
            health.Died += OnDied;
            CombatNoise.Emitted += OnNoise;
        }

        private void OnDisable()
        {
            brains.Remove(this);
            CombatNoise.Emitted -= OnNoise;
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Staggered -= OnStaggered;
                health.Died -= OnDied;
            }
            CancelAttack();
            if (phaseInvulnerable) { phaseInvulnerable = false; health?.PopInvulnerability(); }
        }

        // ───────────────────────────── perception ─────────────────────────────

        private Vector2 Center => motor.Center;
        private Vector2 TargetCenter => targetCollider != null ? (Vector2)targetCollider.bounds.center :
            target != null ? (Vector2)target.transform.position : lastSeen;

        private void Sense()
        {
            if (Time.time < nextSenseAt) return;
            nextSenseAt = Time.time + .1f;
            float radius = Engaged ? config.pursuitRadius : config.sightRadius;
            var candidate = Health.ClosestOpponent(Team.Enemy, Center, radius);
            seeing = false;
            if (candidate == null) return;
            if (candidate != target)
            {
                target = candidate;
                targetCollider = FindBody(candidate);
            }
            seeing = HitQuery.ClearLine(Center, TargetCenter, transform, target.transform);
            if (!seeing) return;
            lastSeen = TargetCenter;
            lastSeenAt = Time.time;
        }

        private static Collider2D FindBody(Component owner)
        {
            foreach (var collider in owner.GetComponentsInChildren<Collider2D>())
                if (!collider.isTrigger) return collider;
            return null;
        }

        private void OnNoise(Vector2 position, float radius, Team source)
        {
            if (source == Team.Enemy || !IsAlive) return;
            if (State != EnemyState.Idle && State != EnemyState.Wander && State != EnemyState.Return) return;
            if ((position - Center).sqrMagnitude > radius * radius * config.hearingMultiplier * config.hearingMultiplier) return;
            lastSeen = position;
            lastSeenAt = Time.time;
            Facing = (position - Center).normalized;
            presentation?.ShowSearch();
            SetState(EnemyState.Search);
        }

        /// <summary>An ally spotted the player: join quickly, without the full reaction pause.</summary>
        public void Notify(Health opponent, Vector2 position)
        {
            if (!IsAlive || Engaged || opponent == null) return;
            if (State == EnemyState.Attack || State == EnemyState.Phase) return;
            target = opponent;
            targetCollider = FindBody(opponent);
            lastSeen = position;
            lastSeenAt = Time.time;
            Engaged = true;
            presentation?.ShowAlert();
            SetState(EnemyState.Alert, config.reactionTime * .5f);
        }

        private void AlertAllies()
        {
            if (config.alertAllies <= 0f) return;
            foreach (var ally in brains)
            {
                if (ally == this || ally.Engaged) continue;
                if ((ally.Center - Center).sqrMagnitude > config.alertAllies * config.alertAllies) continue;
                ally.Notify(target, lastSeen);
            }
        }

        // ───────────────────────────── main loop ─────────────────────────────

        private void FixedUpdate()
        {
            if (config == null) return;
            if (State == EnemyState.Dead) { TickDead(); return; }
            if (State == EnemyState.Downed) { TickDowned(); return; }
            Sense();
            HopPhase = config.hopPeriod > 0f ? Mathf.Repeat(Time.time / config.hopPeriod, 1f) : 0f;
            float elapsed = Time.time - stateStartedAt;
            switch (State)
            {
                case EnemyState.Idle: TickIdle(elapsed); break;
                case EnemyState.Wander: TickWander(elapsed); break;
                case EnemyState.Alert: TickAlert(elapsed); break;
                case EnemyState.Chase: TickChase(); break;
                case EnemyState.Engage: TickEngage(); break;
                case EnemyState.Attack: TickAttack(); break;
                case EnemyState.Stagger: if (elapsed >= stateDuration) AfterStagger(); else motor.Hold(); break;
                case EnemyState.Retreat: TickRetreat(elapsed); break;
                case EnemyState.Search: TickSearch(elapsed); break;
                case EnemyState.Return: TickReturn(); break;
                case EnemyState.Phase: TickPhase(elapsed); break;
            }
        }

        private void SetState(EnemyState next, float duration = 0f)
        {
            if (State == EnemyState.Attack && next != EnemyState.Attack) CancelAttack();
            State = next;
            stateStartedAt = Time.time;
            stateDuration = duration;
            StateChanged?.Invoke(next);
        }

        private bool Spotted => seeing && target != null && !target.IsDead;

        private void Notice()
        {
            Engaged = true;
            Facing = (TargetCenter - Center).normalized;
            presentation?.ShowAlert();
            SetState(EnemyState.Alert, config.reactionTime);
        }

        private void TickIdle(float elapsed)
        {
            motor.Hold();
            if (Spotted) { Notice(); return; }
            if (config.wanderRadius > 0f && elapsed >= stateDuration) PickWanderPoint();
        }

        private void PickWanderPoint()
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector2 candidate = home + Random.insideUnitCircle * config.wanderRadius;
                if (!HitQuery.ClearLine(Center, candidate, transform)) continue;
                moveGoal = candidate;
                SetState(EnemyState.Wander, 4f);
                return;
            }
            SetState(EnemyState.Idle, config.wanderPause);
        }

        private void TickWander(float elapsed)
        {
            if (Spotted) { Notice(); return; }
            Vector2 delta = moveGoal - (Vector2)transform.position;
            if (delta.magnitude < .2f || elapsed > stateDuration)
            {
                SetState(EnemyState.Idle, config.wanderPause * Random.Range(.6f, 1.4f));
                return;
            }
            MoveToward(delta.normalized, config.wanderSpeed);
        }

        private void TickAlert(float elapsed)
        {
            motor.Hold();
            if (target != null) Facing = (TargetCenter - Center).normalized;
            if (elapsed < stateDuration) return;
            AlertAllies();
            SetState(EnemyState.Chase);
        }

        private bool ShouldGiveUp()
        {
            if ((Center - home).sqrMagnitude > config.leashRadius * config.leashRadius && !config.ignoresAttackSlots) return true;
            return target == null || target.IsDead;
        }

        private bool LostTarget => !seeing && Time.time - lastSeenAt > .35f;

        private void TickChase()
        {
            if (ShouldGiveUp()) { GoHome(); return; }
            if (LostTarget) { presentation?.ShowSearch(); SetState(EnemyState.Search, config.memory); return; }
            Vector2 toTarget = TargetCenter - Center;
            Facing = toTarget.normalized;
            if (TryStartAttack()) return;
            float distance = GapToTarget();
            if (distance <= config.engageDistance && !config.ignoresAttackSlots) { SetState(EnemyState.Engage); return; }
            MoveToward(toTarget.normalized, config.chaseSpeed * speedScale);
        }

        private void TickEngage()
        {
            if (ShouldGiveUp()) { GoHome(); return; }
            if (LostTarget) { presentation?.ShowSearch(); SetState(EnemyState.Search, config.memory); return; }
            Vector2 toTarget = TargetCenter - Center;
            float distance = GapToTarget();
            Facing = toTarget.normalized;
            if (TryStartAttack()) return;
            if (distance > config.engageDistance * 1.7f) { SetState(EnemyState.Chase); return; }
            if (Time.time >= strafeSwitchAt)
            {
                strafeSwitchAt = Time.time + Random.Range(1.2f, 2.6f);
                if (Random.value < .45f) strafeSign = -strafeSign;
            }
            Vector2 radial = toTarget.normalized;
            Vector2 tangent = new Vector2(-radial.y, radial.x) * strafeSign;
            // Close in when an attack is ready but out of range; otherwise keep the ring distance.
            float wanted = AttackReadyButFar() ? 0f : config.engageDistance;
            float correction = Mathf.Clamp((distance - wanted) * 1.5f, -1f, 1f);
            Vector2 direction = radial * correction + tangent * config.strafeAmount;
            if (direction.sqrMagnitude < .01f) { motor.Hold(); return; }
            float speed = config.chaseSpeed * speedScale * Mathf.Lerp(.55f, 1f, Mathf.Abs(correction));
            if (HitQuery.ObstacleDistance(Center, tangent, .2f, .6f, transform) < .5f) strafeSign = -strafeSign;
            MoveToward(direction.normalized, speed);
        }

        private bool AttackReadyButFar()
        {
            if (Time.time < nextAttackAt) return false;
            if (!config.ignoresAttackSlots && AttackSlots.Count(target) >= AttackSlots.Capacity && !AttackSlots.Holds(target, this))
                return false;
            for (int i = 0; i < config.attacks.Length; i++)
                if (config.attacks[i] != null && Time.time >= attackReadyAt[i] && PhaseAllows(config.attacks[i])) return true;
            return false;
        }

        private void TickRetreat(float elapsed)
        {
            if (target == null || elapsed >= stateDuration) { SetState(EnemyState.Engage); return; }
            Vector2 away = (Center - TargetCenter).normalized;
            Facing = -away;
            MoveToward(away, config.chaseSpeed * speedScale);
        }

        private void TickSearch(float elapsed)
        {
            if (Spotted) { Engaged = true; SetState(EnemyState.Chase); return; }
            Vector2 delta = lastSeen - Center;
            if (delta.magnitude > .4f && elapsed < stateDuration + 2f) MoveToward(delta.normalized, config.chaseSpeed * .75f);
            else
            {
                motor.Hold();
                // Look around: flip facing every half second.
                if (Mathf.Repeat(elapsed, 1f) < Time.fixedDeltaTime * 1.5f) Facing = new Vector2(-Facing.x, Facing.y);
            }
            if (elapsed > Mathf.Max(stateDuration, 1f) + 2f) GoHome();
        }

        private void GoHome()
        {
            Engaged = false;
            AttackSlots.Release(this);
            SetState(EnemyState.Return);
        }

        private void TickReturn()
        {
            Vector2 delta = home - (Vector2)transform.position;
            if (Spotted && (Center - home).sqrMagnitude < config.leashRadius * config.leashRadius * .5f)
            {
                Engaged = true;
                SetState(EnemyState.Chase);
                return;
            }
            if (delta.magnitude < .25f)
            {
                // Recover between encounters, but never while the fight is fresh.
                if (Time.time - health.LastHitTime > 3f) health.Restore(1f);
                SetState(EnemyState.Idle, config.wanderPause);
                return;
            }
            MoveToward(delta.normalized, config.wanderSpeed * 1.5f);
        }

        private void MoveToward(Vector2 direction, float speed)
        {
            if (config.hopPeriod > 0f)
            {
                // Slime hops: burst of speed in the first 60% of each period, then a short landing pause.
                float pulse = HopPhase < .6f ? Mathf.Sin(HopPhase / .6f * Mathf.PI) * 1.7f : 0f;
                speed *= pulse;
            }
            Facing = Mathf.Abs(direction.x) > .1f ? direction : Facing;
            motor.Move(direction * speed);
        }

        private float GapToTarget()
        {
            if (targetCollider != null && motor.Body != null)
            {
                var distance = motor.Body.Distance(targetCollider);
                if (distance.isValid) return Mathf.Max(0f, distance.distance);
            }
            return Vector2.Distance(Center, TargetCenter);
        }

        // ───────────────────────────── attacks ─────────────────────────────

        private bool PhaseAllows(EnemyAttack candidate) => candidate.minPhase <= phase;

        private bool TryStartAttack()
        {
            if (Time.time < nextAttackAt || target == null || !seeing || motor.InKnockback) return false;
            float gap = GapToTarget();
            float total = 0f;
            for (int i = 0; i < config.attacks.Length; i++)
                if (Usable(i, gap)) total += config.attacks[i].weight;
            if (total <= 0f) return false;
            if (!config.ignoresAttackSlots && !AttackSlots.TryAcquire(target, this)) return false;
            float roll = Random.value * total;
            for (int i = 0; i < config.attacks.Length; i++)
            {
                if (!Usable(i, gap)) continue;
                roll -= config.attacks[i].weight;
                if (roll > 0f) continue;
                BeginAttack(i);
                return true;
            }
            AttackSlots.Release(this);
            return false;
        }

        private bool Usable(int index, float gap)
        {
            var candidate = config.attacks[index];
            if (candidate == null || Time.time < attackReadyAt[index] || !PhaseAllows(candidate)) return false;
            if (candidate.kind == EnemyAttackKind.Summon)
                return config.summonPrefab != null && AliveSummons < config.maxSummons;
            if (candidate.kind == EnemyAttackKind.Volley && config.projectilePrefab == null) return false;
            return gap >= candidate.minRange && gap <= candidate.maxRange;
        }

        private void BeginAttack(int index)
        {
            attackIndex = index;
            attack = config.attacks[index];
            attackDirection = (TargetCenter - Center).normalized;
            if (attackDirection == Vector2.zero) attackDirection = Facing;
            attackPoint = TargetCenter;
            if (attack.kind == EnemyAttackKind.Slam)
            {
                // Land in front of the target, never inside a wall.
                Vector2 from = transform.position;
                Vector2 to = (Vector2)target.transform.position;
                float limit = attack.speed > 0f ? attack.speed : 6f;
                if ((to - from).magnitude > limit) to = from + (to - from).normalized * limit;
                float free = HitQuery.ObstacleDistance(from, (to - from).normalized, motor.BodyRadius * .8f, (to - from).magnitude, transform);
                attackPoint = from + (to - from).normalized * Mathf.Max(0f, free - .05f);
            }
            Facing = attackDirection;
            struck.Clear();
            attackFired = false;
            motor.Hold();
            SetState(EnemyState.Attack);
            SetStage(AttackStage.Windup);
        }

        private void SetStage(AttackStage stage)
        {
            AttackPhase = stage;
            attackStageAt = Time.time;
            if (stage == AttackStage.Active && attack.kind == EnemyAttackKind.Slam) leapFrom = transform.position;
        }

        private void TickAttack()
        {
            if (attack == null) { SetState(EnemyState.Chase); return; }
            float elapsed = Time.time - attackStageAt;
            switch (AttackPhase)
            {
                case AttackStage.Windup:
                    // Lunges track the target for most of the windup, then commit.
                    if (attack.kind == EnemyAttackKind.Lunge && elapsed < attack.windup * .6f && target != null)
                        attackDirection = (TargetCenter - Center).normalized;
                    Facing = attackDirection;
                    motor.Hold();
                    DrawTelegraph(Mathf.Clamp01(elapsed / attack.windup), false);
                    if (elapsed >= attack.windup) { SetStage(AttackStage.Active); CombatNoise.Emit(Center, 3f, Team.Enemy); }
                    break;
                case AttackStage.Active:
                    DrawTelegraph(1f, true);
                    StepActive(Mathf.Clamp01(elapsed / attack.active));
                    if (elapsed >= attack.active) { FinishActive(); SetStage(AttackStage.Recovery); }
                    break;
                case AttackStage.Recovery:
                    telegraph.Hide();
                    Lift = 0f;
                    motor.Hold();
                    if (elapsed >= attack.recovery) EndAttack();
                    break;
            }
        }

        private DamageInfo Damage(Vector2 point) => new DamageInfo(attack.damage, attackDirection, attack.knockback,
            1f, gameObject, Team.Enemy, point, attack.heavy);

        private void StepActive(float t)
        {
            float bodyRadius = motor.BodyRadius;
            switch (attack.kind)
            {
                case EnemyAttackKind.Melee:
                    motor.Hold();
                    HitQuery.Arc(Center, bodyRadius + attack.reach, attackDirection, attack.arc, transform, Team.Enemy, struck, Damage(Center));
                    break;
                case EnemyAttackKind.Lunge:
                    if (HitQuery.ObstacleDistance(Center, attackDirection, bodyRadius * .7f, .25f, transform) < .2f) motor.Hold();
                    else motor.Drive(attackDirection * attack.speed * speedScale);
                    HitQuery.Arc(Center, bodyRadius + attack.reach, attackDirection, 360f, transform, Team.Enemy, struck, Damage(Center));
                    break;
                case EnemyAttackKind.Slam:
                    // Ballistic hop drawn as lift; the body glides to the landing point.
                    Vector2 next = Vector2.Lerp(leapFrom, attackPoint, Mathf.SmoothStep(0f, 1f, t));
                    motor.Drive((next - (Vector2)transform.position) / Time.fixedDeltaTime);
                    Lift = Mathf.Sin(t * Mathf.PI) * Mathf.Clamp(Vector2.Distance(leapFrom, attackPoint) * .35f, .6f, 1.6f);
                    break;
                case EnemyAttackKind.Volley:
                case EnemyAttackKind.Summon:
                    motor.Hold();
                    if (!attackFired) { attackFired = true; Fire(); }
                    break;
            }
        }

        private void FinishActive()
        {
            if (attack.kind != EnemyAttackKind.Slam) return;
            Lift = 0f;
            motor.Hold();
            Vector2 impact = (Vector2)transform.position + new Vector2(0f, motor.BodyRadius * .2f);
            HitQuery.Arc(impact, attack.reach, Vector2.up, 360f, transform, Team.Enemy, struck, Damage(impact));
            CameraShake.Add(attack.heavy ? .55f : .35f);
            HitStop.Request(.05f);
            CombatVfx.Play(VfxKind.Shockwave, impact, Vector2.zero, new Color(.7f, 1f, .55f, .8f));
            CombatVfx.Play(VfxKind.Dust, impact, Vector2.zero, new Color(.7f, .66f, .5f, .85f), 12);
        }

        private void Fire()
        {
            if (attack.kind == EnemyAttackKind.Volley)
            {
                float baseAngle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
                for (int i = 0; i < attack.count; i++)
                {
                    float offset = attack.count == 1 ? 0f : Mathf.Lerp(-attack.spread * .5f, attack.spread * .5f, i / (attack.count - 1f));
                    float angle = (baseAngle + offset) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var projectile = Instantiate(config.projectilePrefab, Center + direction * (motor.BodyRadius + .1f), Quaternion.identity);
                    projectile.Launch(direction, attack.speed, attack.reach, Damage(Center));
                }
                return;
            }
            int amount = Mathf.Min(attack.count, config.maxSummons - AliveSummons);
            for (int i = 0; i < amount; i++)
            {
                Vector2 offset = Quaternion.Euler(0, 0, 360f * i / Mathf.Max(1, amount) + Random.Range(-25f, 25f)) * Vector2.right * 1.6f;
                Vector2 point = Center + offset;
                if (!HitQuery.ClearLine(Center, point, transform)) point = Center + offset * .5f;
                var minion = Instantiate(config.summonPrefab, point, Quaternion.identity);
                summons.Add(minion);
                CombatVfx.Play(VfxKind.SlimeSplash, point, Vector2.zero, new Color(.55f, 1f, .45f, .9f), 10);
                var brain = minion.GetComponent<EnemyBrain>();
                if (brain != null) brain.Notify(target, TargetCenter);
            }
        }

        private void DrawTelegraph(float progress, bool active)
        {
            if (telegraph == null) return;
            float radius = motor.BodyRadius;
            switch (attack.kind)
            {
                case EnemyAttackKind.Melee: telegraph.Sector(Center, attackDirection, radius + attack.reach, attack.arc, progress, active); break;
                case EnemyAttackKind.Lunge:
                    float length = attack.speed * attack.active + radius + attack.reach;
                    if (!active) telegraph.Lane(Center, attackDirection, length, (radius + attack.reach) * 2f, progress, false);
                    else telegraph.Hide();
                    break;
                case EnemyAttackKind.Slam: telegraph.Ring(attackPoint, attack.reach, progress, active); break;
                default: telegraph.Hide(); break;
            }
        }

        private void EndAttack()
        {
            float cooldownScale = 1f / speedScale;
            if (attackIndex >= 0) attackReadyAt[attackIndex] = Time.time + attack.cooldown * cooldownScale;
            nextAttackAt = Time.time + .3f * cooldownScale;
            CancelAttack();
            SetState(retreatPending ? EnemyState.Retreat : (config.ignoresAttackSlots ? EnemyState.Chase : EnemyState.Engage),
                retreatPending ? config.retreatTime : 0f);
            retreatPending = false;
        }

        private void CancelAttack()
        {
            if (attack != null && attackIndex >= 0 && AttackPhase != AttackStage.None && AttackPhase != AttackStage.Recovery)
                attackReadyAt[attackIndex] = Mathf.Max(attackReadyAt[attackIndex], Time.time + attack.cooldown * .5f);
            attack = null;
            attackIndex = -1;
            AttackPhase = AttackStage.None;
            Lift = 0f;
            if (telegraph != null) telegraph.Hide();
            AttackSlots.Release(this);
        }

        // ───────────────────────────── damage ─────────────────────────────

        private void OnDamaged(DamageInfo info, HitResult result)
        {
            presentation?.Hit(config.hitFlash);
            float resist = Mathf.Clamp01(config.knockbackResistance);
            bool armored = State == EnemyState.Attack && attack != null && (attack.superArmor || AttackPhase == AttackStage.Active);
            if (!armored && info.Knockback > 0f) motor.Knockback(info.Direction * info.Knockback * (1f - resist), .16f);
            if (result == HitResult.Killed) return;
            if (!Engaged)
            {
                var source = info.Source != null ? info.Source.GetComponentInParent<Health>() : null;
                target = source != null ? source : Health.ClosestOpponent(Team.Enemy, Center, config.pursuitRadius);
                if (target != null) { targetCollider = FindBody(target); lastSeen = TargetCenter; lastSeenAt = Time.time; }
                Engaged = true;
                AlertAllies();
            }
            if (CheckPhase()) return;
            if (State == EnemyState.Attack && AttackPhase == AttackStage.Windup && !attack.superArmor)
            {
                SetState(EnemyState.Stagger, .22f);
                return;
            }
            if (State == EnemyState.Attack) { if (Random.value < config.retreatOnHit) retreatPending = true; return; }
            if (State == EnemyState.Phase || State == EnemyState.Stagger) return;
            if (Random.value < config.retreatOnHit) { SetState(EnemyState.Retreat, config.retreatTime); return; }
            if (State != EnemyState.Engage) SetState(EnemyState.Chase);
        }

        private void OnStaggered(DamageInfo info)
        {
            if (!IsAlive || State == EnemyState.Phase) return;
            bool armored = State == EnemyState.Attack && attack != null && attack.superArmor && AttackPhase == AttackStage.Active;
            if (armored) return;
            motor.Knockback(info.Direction * Mathf.Max(info.Knockback, 3f) * (1f - Mathf.Clamp01(config.knockbackResistance * .5f)), .2f);
            SetState(EnemyState.Stagger, config.staggerDuration);
        }

        private void AfterStagger()
        {
            if (Random.value < config.retreatOnHit) SetState(EnemyState.Retreat, config.retreatTime);
            else SetState(EnemyState.Chase);
        }

        private bool CheckPhase()
        {
            if (config.phaseThresholds == null || phase - 1 >= config.phaseThresholds.Length) return false;
            if (health.Normalized > config.phaseThresholds[phase - 1]) return false;
            phase++;
            speedScale = Mathf.Pow(config.phaseSpeedUp, phase - 1);
            SetState(EnemyState.Phase, config.phaseTransitionTime);
            if (!phaseInvulnerable) { phaseInvulnerable = true; health.PushInvulnerability(); }
            motor.Stop();
            CameraShake.Add(.6f);
            HitStop.Request(.08f);
            CombatVfx.Play(VfxKind.Shockwave, Center, Vector2.zero, new Color(.75f, 1f, .5f, .9f));
            // Roar: shove the player away without damage so the new phase starts with space.
            foreach (var other in Health.Active)
            {
                if (other.Team != Team.Player) continue;
                Vector2 away = (Vector2)other.transform.position - Center;
                if (away.magnitude > config.phaseShockwave) continue;
                var pushable = other.GetComponent<IPushable>();
                pushable?.Push(away.normalized * 9f, .3f);
            }
            PhaseChanged?.Invoke(phase);
            return true;
        }

        private void TickPhase(float elapsed)
        {
            motor.Hold();
            if (elapsed < stateDuration) return;
            if (phaseInvulnerable) { phaseInvulnerable = false; health.PopInvulnerability(); }
            // A new phase always opens with its signature attack available.
            for (int i = 0; i < attackReadyAt.Length; i++) if (config.attacks[i].minPhase == phase) attackReadyAt[i] = 0f;
            nextAttackAt = Time.time + .2f;
            SetState(EnemyState.Chase);
        }

        private void OnDied(DamageInfo info)
        {
            CancelAttack();
            motor.Stop();
            CameraShake.Add(config.ignoresAttackSlots ? .8f : .2f);
            if (config.ignoresAttackSlots) HitStop.Request(.15f);
            if (revivesLeft > 0)
            {
                presentation?.SetAlive(false, true);
                body.simulated = false;
                SetState(EnemyState.Downed, config.reviveDelay + 4f);
                CombatVfx.Play(VfxKind.Dust, Center, Vector2.zero, new Color(.85f, .82f, .7f, .9f), 8);
                return;
            }
            presentation?.SetAlive(false, false);
            body.simulated = false;
            deathAt = Time.time;
            Engaged = false;
            SetState(EnemyState.Dead);
            CombatVfx.Play(VfxKind.DeathBurst, Center, Vector2.zero, config.deathTint);
            foreach (var go in deactivateOnDefeat) if (go != null) go.SetActive(false);
            foreach (var minion in summons) if (minion != null) minion.GetComponent<Health>()?.Kill(gameObject);
            onDefeated?.Invoke();
            Defeated?.Invoke();
        }

        /// <summary>Animation event at the end of the revive clip; also used as a fallback timer.</summary>
        public void Revive()
        {
            if (State != EnemyState.Downed) return;
            revivesLeft--;
            body.simulated = true;
            health.Restore(config.reviveHealth);
            presentation?.SetAlive(true, revivesLeft > 0);
            presentation?.ShowAlert();
            CombatVfx.Play(VfxKind.Dust, Center, Vector2.zero, new Color(.85f, .82f, .7f, .9f), 10);
            Engaged = true;
            SetState(EnemyState.Chase);
        }

        private void TickDowned()
        {
            if (Time.time - stateStartedAt >= stateDuration) Revive();
        }

        /// <summary>Legacy animation event of the slime death clip: start fading right away.</summary>
        public void OnObjectDestroyed() { if (State == EnemyState.Dead) deathAt = Mathf.Min(deathAt, Time.time - .6f); }

        private void TickDead()
        {
            float t = (Time.time - deathAt - .6f) / .7f;
            if (t <= 0f) return;
            presentation?.SetFade(t);
            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            if (config == null) return;
            Vector3 origin = Application.isPlaying ? (Vector3)home : transform.position;
            Gizmos.color = new Color(1f, .9f, .2f, .5f);
            Gizmos.DrawWireSphere(transform.position, config.sightRadius);
            Gizmos.color = new Color(.3f, .7f, 1f, .4f);
            Gizmos.DrawWireSphere(origin, config.leashRadius);
        }
    }
}
