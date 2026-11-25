using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    public enum VfxKind { HitSpark, Dust, DeathBurst, SlimeSplash, Shockwave }

    /// <summary>
    /// Scene-level particle emitters for combat. Each kind is one world-space ParticleSystem that
    /// receives Emit() calls, so there is no pooling or per-hit instantiation. Missing emitters are
    /// silently skipped, which keeps gameplay testable without visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatVfx : MonoBehaviour
    {
        private static CombatVfx instance;
        [SerializeField] private ParticleSystem hitSpark;
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private ParticleSystem deathBurst;
        [SerializeField] private ParticleSystem slimeSplash;
        [SerializeField] private ParticleSystem shockwave;

        public static int Emitted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; Emitted = 0; }

        private void OnEnable() => instance = this;
        private void OnDisable() { if (instance == this) instance = null; }

        public static void Play(VfxKind kind, Vector2 position, Vector2 direction, Color tint, int count = -1)
        {
            Emitted++;
            if (instance == null) return;
            var system = instance.Get(kind);
            if (system == null) return;
            int amount = count > 0 ? count : DefaultCount(kind);
            var main = system.main;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true, startColor = tint };
            float baseSpeed = main.startSpeed.constantMax;
            for (int i = 0; i < amount; i++)
            {
                emit.position = position;
                if (direction.sqrMagnitude > .001f && kind == VfxKind.HitSpark)
                {
                    // Sparks fly away from the attacker in a 100 degree fan.
                    float angle = Mathf.Atan2(direction.y, direction.x) + Random.Range(-.87f, .87f);
                    emit.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * baseSpeed * Random.Range(.5f, 1.1f);
                }
                else emit.ResetVelocity();
                system.Emit(emit, 1);
            }
        }

        private static int DefaultCount(VfxKind kind) => kind switch
        {
            VfxKind.HitSpark => 12,
            VfxKind.Dust => 6,
            VfxKind.DeathBurst => 22,
            VfxKind.SlimeSplash => 14,
            VfxKind.Shockwave => 1,
            _ => 4
        };

        private ParticleSystem Get(VfxKind kind) => kind switch
        {
            VfxKind.HitSpark => hitSpark,
            VfxKind.Dust => dust,
            VfxKind.DeathBurst => deathBurst,
            VfxKind.SlimeSplash => slimeSplash,
            VfxKind.Shockwave => shockwave,
            _ => null
        };
    }
}
