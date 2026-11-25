using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    [CreateAssetMenu(menuName = "Mystic Woods/Player/Configuration")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Movement (world units / seconds)")]
        [SerializeField, Min(.01f)] private float walkSpeed = 3.5f;
        [SerializeField, Min(.01f)] private float runSpeed = 5.25f;
        [SerializeField, Min(.01f)] private float accelerationTime = .12f;
        [SerializeField, Min(.01f)] private float brakingTime = .08f;
        [Header("Presentation")]
        [SerializeField, Min(.001f)] private float movingThreshold = .03f;
        [SerializeField, Range(0f, .1f)] private float directionHysteresis = .03f;
        [SerializeField, Range(0f, .03f)] private float idleBreathing = .015f;
        [SerializeField, Min(.1f)] private float breathingPeriod = 1.8f;
        public float WalkSpeed => Mathf.Max(.01f, walkSpeed);
        public float RunSpeed => Mathf.Max(WalkSpeed, runSpeed);
        public float AccelerationTime => Mathf.Max(.01f, accelerationTime);
        public float BrakingTime => Mathf.Max(.01f, brakingTime);
        public float MovingThreshold => Mathf.Max(.001f, movingThreshold);
        public float DirectionHysteresis => directionHysteresis;
        public float IdleBreathing => idleBreathing;
        public float BreathingPeriod => Mathf.Max(.1f, breathingPeriod);
    }
}
