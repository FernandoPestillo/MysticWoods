using System.Collections.Generic;
using MysticWoods.Gameplay.Combat;
using UnityEngine;

namespace MysticWoods.Gameplay.Enemies
{
    /// <summary>Slow, readable slime orb. Hits opponents once; breaks on scenery.</summary>
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private readonly HashSet<IHittable> struck = new HashSet<IHittable>();
        private Vector2 velocity;
        private float radius;
        private float dieAt;
        private DamageInfo damage;
        private Team team;

        public static int Alive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Alive = 0;

        public void Launch(Vector2 direction, float speed, float hitRadius, in DamageInfo info, float lifetime = 3.2f)
        {
            velocity = direction.normalized * speed;
            radius = hitRadius;
            damage = info;
            team = info.SourceTeam;
            dieAt = Time.time + lifetime;
        }

        private void OnEnable() => Alive++;
        private void OnDisable() => Alive--;

        private void FixedUpdate()
        {
            Vector2 from = transform.position;
            Vector2 to = from + velocity * Time.fixedDeltaTime;
            if (Time.time >= dieAt || !HitQuery.ClearLine(from, to))
            {
                Break();
                return;
            }
            transform.position = to;
            var info = damage;
            info.Direction = velocity.normalized;
            if (HitQuery.Arc(to, radius, velocity, 360f, null, team, struck, info) > 0)
            {
                CameraShake.Add(.18f);
                Break();
            }
        }

        private void Break()
        {
            CombatVfx.Play(VfxKind.SlimeSplash, transform.position, Vector2.zero, new Color(.55f, 1f, .45f, .9f), 8);
            Destroy(gameObject);
        }
    }
}
