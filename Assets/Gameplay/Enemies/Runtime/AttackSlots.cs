using System.Collections.Generic;
using UnityEngine;

namespace MysticWoods.Gameplay.Enemies
{
    /// <summary>
    /// Limits how many enemies attack the same target at once. Waiting enemies circle instead
    /// of stacking on the player, which keeps every incoming attack readable.
    /// </summary>
    public static class AttackSlots
    {
        private static readonly Dictionary<Object, List<Object>> holders = new Dictionary<Object, List<Object>>();
        private static readonly Dictionary<Object, float> lastAttackAt = new Dictionary<Object, float>();

        /// <summary>Simultaneous attackers per target.</summary>
        public static int Capacity { get; set; } = 2;
        /// <summary>Minimum time between two attack starts against the same target.</summary>
        public static float Stagger { get; set; } = .35f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            holders.Clear();
            lastAttackAt.Clear();
            Capacity = 2;
            Stagger = .35f;
        }

        public static bool TryAcquire(Object target, Object attacker)
        {
            if (target == null || attacker == null) return false;
            var list = Holders(target);
            if (list.Contains(attacker)) return true;
            if (list.Count >= Capacity) return false;
            if (lastAttackAt.TryGetValue(target, out float last) && Time.time - last < Stagger) return false;
            list.Add(attacker);
            lastAttackAt[target] = Time.time;
            return true;
        }

        public static void Release(Object attacker)
        {
            foreach (var list in holders.Values) list.Remove(attacker);
        }

        public static int Count(Object target) => target != null && holders.TryGetValue(target, out var list) ? Prune(list) : 0;
        public static bool Holds(Object target, Object attacker) =>
            target != null && holders.TryGetValue(target, out var list) && list.Contains(attacker);

        private static List<Object> Holders(Object target)
        {
            if (!holders.TryGetValue(target, out var list)) holders[target] = list = new List<Object>(4);
            Prune(list);
            return list;
        }

        private static int Prune(List<Object> list)
        {
            list.RemoveAll(x => x == null);
            return list.Count;
        }
    }
}
