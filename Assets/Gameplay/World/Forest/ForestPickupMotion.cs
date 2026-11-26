using UnityEngine;

namespace MysticWoods.Environment
{
    public sealed class ForestPickupMotion : MonoBehaviour
    {
        [SerializeField] private float height = .13f;
        [SerializeField] private float speed = 2.2f;
        private Vector3 initialPosition;
        private void Awake() => initialPosition = transform.localPosition;
        private void Update() => transform.localPosition = initialPosition + Vector3.up * (Mathf.Sin(Time.time * speed) * height);
    }
}
