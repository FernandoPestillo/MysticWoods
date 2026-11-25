using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    public enum Team { Neutral, Player, Enemy }

    public enum HitResult { Ignored, Damaged, Killed }

    /// <summary>One hit, described once by the attacker and interpreted by the receiver.</summary>
    public struct DamageInfo
    {
        /// <summary>Health removed.</summary>
        public float Amount;
        /// <summary>Normalized direction from attacker to victim.</summary>
        public Vector2 Direction;
        /// <summary>Initial knockback speed, in world units per second.</summary>
        public float Knockback;
        /// <summary>Stagger build-up. A victim whose poise reaches zero is interrupted.</summary>
        public float Poise;
        /// <summary>Heavy hits trigger stronger hit-stop and camera feedback.</summary>
        public bool Heavy;
        public Vector2 Point;
        public GameObject Source;
        public Team SourceTeam;

        public DamageInfo(float amount, Vector2 direction, float knockback, float poise, GameObject source, Team team,
            Vector2 point, bool heavy = false)
        {
            Amount = amount;
            Direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.zero;
            Knockback = knockback;
            Poise = poise;
            Source = source;
            SourceTeam = team;
            Point = point;
            Heavy = heavy;
        }
    }

    public interface IHittable
    {
        Team Team { get; }
        bool CanBeHit { get; }
        Transform transform { get; }
        HitResult ReceiveHit(in DamageInfo info);
    }

    /// <summary>Receives damage-free pushes (boss roars, shockwaves).</summary>
    public interface IPushable
    {
        void Push(Vector2 velocity, float duration);
    }
}
