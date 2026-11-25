using UnityEngine;
using UnityEngine.InputSystem;

namespace MysticWoods.Gameplay.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        private InputActionAsset instance;
        private InputAction move;
        private InputAction run;
        private bool hasFocus = true;
        private bool CanRead => isActiveAndEnabled && hasFocus && instance != null;
        public Vector2 Move => CanRead ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public bool Run => CanRead && run.IsPressed();

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("PlayerInputReader requires its input actions asset.", this);
                enabled = false;
                return;
            }
            // Each character owns its action instance; disabling one cannot affect another.
            instance = Instantiate(inputActions);
            move = instance.FindAction("Player/Move", true);
            run = instance.FindAction("Player/Run", true);
        }
        private void OnEnable() { if (instance != null) instance.Enable(); }
        private void OnDisable() { if (instance != null) instance.Disable(); }
        private void OnApplicationFocus(bool focused) => hasFocus = focused;
        private void OnDestroy() { if (instance != null) Destroy(instance); }
    }
}
