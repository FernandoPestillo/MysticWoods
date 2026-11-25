using System.Collections.Generic;
using MysticWoods.Gameplay.Combat;
using UnityEngine;

namespace MysticWoods.Gameplay.Enemies
{
    /// <summary>
    /// Velocity-based locomotion with context steering: the desired direction is scored against
    /// sixteen candidates, penalising static obstacles ahead and crowding allies, so enemies slide
    /// around trees and fences instead of pushing into them. Knockback temporarily overrides steering.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyMotor : MonoBehaviour
    {
        private const int Directions = 16;
        private static readonly Vector2[] candidates = BuildCandidates();
        private static readonly List<EnemyMotor> motors = new List<EnemyMotor>();
        [SerializeField, Min(.1f)] private float lookAhead = 1.1f;
        [SerializeField, Min(.02f)] private float probeRadius = .22f;
        private Rigidbody2D body;
        private Collider2D bodyCollider;
        private Vector2 velocity;
        private Vector2 knockback;
        private float knockbackStart, knockbackDuration = .01f, knockbackUntil;
        private float[] danger = new float[Directions];
        private int probeCursor;
        private Vector2 lastSteer;

        public float Acceleration { get; set; } = 14f;
        public float Separation { get; set; } = .7f;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public bool InKnockback => Time.time < knockbackUntil;
        public Vector2 Center => bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
        public float BodyRadius => bodyCollider != null ? Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) : .3f;
        public Collider2D Body => bodyCollider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => motors.Clear();

        private static Vector2[] BuildCandidates()
        {
            var result = new Vector2[Directions];
            for (int i = 0; i < Directions; i++)
            {
                float angle = i * Mathf.PI * 2f / Directions;
                result[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return result;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = 0f;
        }

        private void OnEnable() => motors.Add(this);
        private void OnDisable()
        {
            motors.Remove(this);
            Stop();
        }

        /// <summary>Steer toward <paramref name="desired"/> (direction * speed), avoiding obstacles.</summary>
        public void Move(Vector2 desired, bool avoid = true)
        {
            if (InKnockback) { ApplyKnockback(); return; }
            float speed = desired.magnitude;
            Vector2 target = desired;
            if (avoid && speed > .01f) target = Steer(desired / speed) * speed;
            velocity = Vector2.MoveTowards(body.linearVelocity, target, Acceleration * Time.fixedDeltaTime);
            body.linearVelocity = velocity;
        }

        /// <summary>Drive this exact velocity (dash, leap). No avoidance.</summary>
        public void Drive(Vector2 exact)
        {
            if (InKnockback) { ApplyKnockback(); return; }
            velocity = exact;
            body.linearVelocity = exact;
        }

        public void Hold() => Move(Vector2.zero, false);

        public void Knockback(Vector2 push, float duration)
        {
            knockback = push;
            knockbackStart = Time.time;
            knockbackDuration = Mathf.Max(.02f, duration);
            knockbackUntil = Time.time + knockbackDuration;
            body.linearVelocity = push;
        }

        public void Stop()
        {
            velocity = Vector2.zero;
            knockbackUntil = 0f;
            if (body != null && body.simulated) body.linearVelocity = Vector2.zero;
        }

        private void ApplyKnockback()
        {
            float t = Mathf.Clamp01((Time.time - knockbackStart) / knockbackDuration);
            body.linearVelocity = knockback * (1f - t) * (1f - t);
        }

        private Vector2 Steer(Vector2 desired)
        {
            // Probe a few directions per step; the danger map updates over ~4 physics steps.
            Vector2 origin = Center;
            for (int n = 0; n < 4; n++)
            {
                int i = probeCursor = (probeCursor + 1) % Directions;
                float free = HitQuery.ObstacleDistance(origin, candidates[i], probeRadius, lookAhead, transform);
                danger[i] = 1f - free / lookAhead;
            }
            Vector2 separation = Vector2.zero;
            foreach (var other in motors)
            {
                if (other == this) continue;
                Vector2 away = origin - other.Center;
                float distance = away.magnitude;
                float range = Separation + other.BodyRadius;
                if (distance < .001f || distance > range) continue;
                separation += away / distance * (1f - distance / range);
            }
            Vector2 best = desired;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < Directions; i++)
            {
                float interest = Vector2.Dot(candidates[i], desired) + .25f * Vector2.Dot(candidates[i], separation)
                    + .15f * Vector2.Dot(candidates[i], lastSteer);
                float score = interest - danger[i] * 1.6f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidates[i];
            }
            // Free path straight ahead: keep the exact direction instead of snapping to 22.5 degree steps.
            int aligned = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(desired.y, desired.x) / (Mathf.PI * 2f), 1f) * Directions) % Directions;
            if (danger[aligned] < .05f && separation.sqrMagnitude < .01f) best = desired;
            lastSteer = Vector2.Lerp(lastSteer, best, .5f);
            return best.normalized;
        }

        /// <summary>Allies around a point; used for alerts.</summary>
        public static IReadOnlyList<EnemyMotor> All => motors;
    }
}
