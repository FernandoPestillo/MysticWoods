using System;
using System.Collections.Generic;
using MysticWoods.Gameplay.Combat;
using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    /// <summary>
    /// Executes sword swings chosen by <see cref="PlayerController"/>: opens the hit window,
    /// resolves targets once per swing and emits impact feedback. Does not read input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;
        [Tooltip("Body centre relative to the feet, where the sword box starts.")]
        [SerializeField] private Vector2 bodyCenter = new Vector2(0f, .42f);
        private readonly HashSet<IHittable> struck = new HashSet<IHittable>();
        private readonly List<IHittable> hitThisStep = new List<IHittable>();
        private PlayerSwing swing;
        private float startedAt;

        public event Action<int> SwingStarted;
        /// <summary>Raised for every target hit (target, swing index).</summary>
        public event Action<IHittable, int> TargetHit;

        public int SwingIndex { get; private set; } = -1;
        public PlayerFacing Direction { get; private set; }
        public bool Swinging => SwingIndex >= 0;
        public float Elapsed => Swinging ? Time.time - startedAt : 0f;
        public float Duration => swing != null ? swing.duration : 0f;
        public float Progress => Swinging ? Mathf.Clamp01(Elapsed / swing.duration) : 0f;
        public bool HitWindowOpen => Swinging && Elapsed >= swing.hitStart && Elapsed < swing.hitEnd;
        public bool CanChain => Swinging && Elapsed >= swing.chainFrom;
        public bool Finished => Swinging && Elapsed >= swing.duration;
        public int TotalHits { get; private set; }
        public PlayerConfig Config => config;
        public Vector2 BodyCenter => bodyCenter;

        public void Begin(int index, PlayerFacing direction)
        {
            swing = config.Swing(index);
            SwingIndex = index;
            Direction = direction;
            startedAt = Time.time;
            struck.Clear();
            CombatNoise.Emit(transform.position, config.SwingNoiseRadius, Team.Player);
            SwingStarted?.Invoke(index);
        }

        public void End()
        {
            SwingIndex = -1;
            swing = null;
            struck.Clear();
        }

        /// <summary>Forward velocity during the early part of a swing; zero afterwards.</summary>
        public Vector2 LungeVelocity()
        {
            if (!Swinging || Elapsed > swing.hitStart + .04f) return Vector2.zero;
            return PlayerDirection.ToVector(Direction) * swing.lunge;
        }

        /// <summary>Samples the sword area; call every physics step while swinging.</summary>
        public void Step()
        {
            if (!HitWindowOpen) return;
            Vector2 forward = PlayerDirection.ToVector(Direction);
            Vector2 origin = (Vector2)transform.position + bodyCenter;
            Vector2 center = origin + forward * (swing.reach * .5f);
            Vector2 size = Mathf.Abs(forward.x) > 0f ? new Vector2(swing.reach, swing.width) : new Vector2(swing.width, swing.reach);
            var template = new DamageInfo(swing.damage, forward, swing.knockback, swing.poise, gameObject, Team.Player,
                center, swing.heavy);
            hitThisStep.Clear();
            int hits = HitQuery.Box(center, size, 0f, transform, Team.Player, struck, template, hitThisStep);
            if (hits == 0) return;
            TotalHits += hits;
            foreach (var target in hitThisStep)
            {
                Vector2 point = target.transform.position + (Vector3)(bodyCenter * .8f);
                CombatVfx.Play(VfxKind.HitSpark, point, forward, swing.heavy ? new Color(1f, .82f, .45f) : new Color(1f, .95f, .8f));
                TargetHit?.Invoke(target, SwingIndex);
            }
            HitStop.Request(swing.hitStop * (hits > 1 ? 1.25f : 1f));
            CameraShake.Add(swing.shake);
        }

        private void OnDisable() => End();

        private void OnDrawGizmosSelected()
        {
            if (config == null || config.ComboLength == 0) return;
            var s = swing ?? config.Swing(0);
            Vector2 forward = PlayerDirection.ToVector(Swinging ? Direction : PlayerFacing.Down);
            Vector2 origin = (Vector2)transform.position + bodyCenter;
            Vector2 size = Mathf.Abs(forward.x) > 0f ? new Vector2(s.reach, s.width) : new Vector2(s.width, s.reach);
            Gizmos.color = HitWindowOpen ? Color.red : Color.yellow;
            Gizmos.DrawWireCube(origin + forward * (s.reach * .5f), size);
        }
    }
}
