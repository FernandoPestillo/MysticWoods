using UnityEngine;

namespace MysticWoods.Gameplay.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMovement))]
    public sealed class PlayerController : MonoBehaviour
    {
        private PlayerInputReader input;
        private PlayerMovement movement;
        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            movement = GetComponent<PlayerMovement>();
        }
        private void Update() => movement.SetCommand(input.Move, input.Run);
        private void OnDisable() { if (movement != null) movement.Stop(); }
    }
}
