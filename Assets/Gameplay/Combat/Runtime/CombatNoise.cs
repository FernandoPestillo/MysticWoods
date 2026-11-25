using System;
using UnityEngine;

namespace MysticWoods.Gameplay.Combat
{
    /// <summary>Sounds the AI can hear (sword swings, impacts). No audio is played here.</summary>
    public static class CombatNoise
    {
        public static event Action<Vector2, float, Team> Emitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Emitted = null;

        public static void Emit(Vector2 position, float radius, Team source) => Emitted?.Invoke(position, radius, source);
    }
}
