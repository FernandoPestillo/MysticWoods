using System.Collections.Generic;
using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>Shape queries that resolve hittable opponents once per swing.</summary>
    public static class HitQuery
    {
        private static readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private static readonly List<RaycastHit2D> casts = new List<RaycastHit2D>(16);
        private static ContactFilter2D solids = CreateSolidFilter();

        private static ContactFilter2D CreateSolidFilter()
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.AllLayers);
            return filter;
        }

        /// <summary>Hit every opponent in a box. Returns how many new targets were hit.</summary>
        public static int Box(Vector2 center, Vector2 size, float angle, Transform owner, Team team,
            HashSet<IHittable> struck, in DamageInfo template, List<IHittable> results = null)
        {
            overlaps.Clear();
            Physics2D.OverlapBox(center, size, angle, solids, overlaps);
            return Resolve(owner, team, struck, template, results, null, 0f, Vector2.zero);
        }

        /// <summary>Hit every opponent inside a circle sector. arc >= 360 is a full circle.</summary>
        public static int Arc(Vector2 origin, float radius, Vector2 direction, float arc, Transform owner, Team team,
            HashSet<IHittable> struck, in DamageInfo template, List<IHittable> results = null)
        {
            overlaps.Clear();
            Physics2D.OverlapCircle(origin, radius, solids, overlaps);
            return Resolve(owner, team, struck, template, results, origin, arc, direction);
        }

        private static int Resolve(Transform owner, Team team, HashSet<IHittable> struck, in DamageInfo template,
            List<IHittable> results, Vector2? arcOrigin, float arc, Vector2 arcDirection)
        {
            int count = 0;
            foreach (var other in overlaps)
            {
                if (other == null || other.isTrigger || (owner != null && other.transform.IsChildOf(owner))) continue;
                var target = other.GetComponentInParent<IHittable>();
                if (target == null || target.Team == team || !target.CanBeHit || struck.Contains(target)) continue;
                Vector2 center = other.bounds.center;
                if (arcOrigin.HasValue && arc < 360f)
                {
                    Vector2 toTarget = center - arcOrigin.Value;
                    // Overlapping bodies always count: the sector apex is inside the attacker.
                    if (toTarget.sqrMagnitude > .04f && Vector2.Angle(arcDirection, toTarget) > arc * .5f) continue;
                }
                struck.Add(target);
                Vector2 origin = owner != null ? (Vector2)owner.position : (arcOrigin ?? center);
                Vector2 direction = center - origin;
                if (direction.sqrMagnitude < .0001f) direction = template.Direction;
                var info = template;
                info.Direction = direction.normalized;
                info.Point = other.ClosestPoint(origin);
                if (target.ReceiveHit(info) == HitResult.Ignored) continue;
                results?.Add(target);
                count++;
            }
            return count;
        }

        /// <summary>True when no static (non-character) collider blocks the segment.</summary>
        public static bool ClearLine(Vector2 from, Vector2 to, Transform ignoreA = null, Transform ignoreB = null)
        {
            casts.Clear();
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < .001f) return true;
            Physics2D.Raycast(from, delta / distance, solids, casts, distance);
            foreach (var hit in casts)
            {
                if (IsCharacter(hit.collider)) continue;
                if (ignoreA != null && hit.collider.transform.IsChildOf(ignoreA)) continue;
                if (ignoreB != null && hit.collider.transform.IsChildOf(ignoreB)) continue;
                return false;
            }
            return true;
        }

        /// <summary>Distance to the first static obstacle along a fat ray, or maxDistance.</summary>
        public static float ObstacleDistance(Vector2 from, Vector2 direction, float radius, float maxDistance, Transform ignore)
        {
            casts.Clear();
            Physics2D.CircleCast(from, radius, direction, solids, casts, maxDistance);
            float best = maxDistance;
            foreach (var hit in casts)
            {
                if (IsCharacter(hit.collider)) continue;
                if (ignore != null && hit.collider.transform.IsChildOf(ignore)) continue;
                best = Mathf.Min(best, hit.distance);
            }
            return best;
        }

        /// <summary>Characters own dynamic bodies; scenery is static or has no body at all.</summary>
        public static bool IsCharacter(Collider2D collider) =>
            collider.attachedRigidbody != null && collider.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic;
    }
}
