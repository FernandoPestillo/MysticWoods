using UnityEngine;
using UnityEngine.InputSystem;

namespace MysticWoods.Gameplay.Player
{
    /// <summary>
    /// Owns the character's input actions. Button presses are latched with a timestamp so the
    /// controller can buffer them; presses made while the game is paused are discarded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        private InputActionAsset instance;
        private InputAction move;
        private InputAction run;
        private InputAction attack;
        private InputAction dodge;
        private bool hasFocus = true;
        private bool suppressed;
        private float attackPressedAt = float.NegativeInfinity;
        private float dodgePressedAt = float.NegativeInfinity;
        private bool CanRead => isActiveAndEnabled && hasFocus && instance != null;
        public Vector2 Move => CanRead ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public bool Run => CanRead && run.IsPressed();
        public bool Suppressed => suppressed;
        /// <summary>Direction held when Attack was pressed (zero if none), so the swing aims where the player meant.</summary>
        public Vector2 AttackAim { get; private set; }
        public Vector2 DodgeAim { get; private set; }

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
            attack = instance.FindAction("Player/Attack", false);
            dodge = instance.FindAction("Player/Dodge", false);
        }

        private void Update()
        {
            if (!CanRead) return;
            if (Time.timeScale <= 0f) { ClearBuffered(); return; }
            if (suppressed)
            {
                // A click/space that closed a menu must not become a swing.
                ClearBuffered();
                if ((attack == null || !attack.IsPressed()) && (dodge == null || !dodge.IsPressed())) suppressed = false;
                return;
            }
            if (attack != null && attack.WasPressedThisFrame()) { attackPressedAt = Time.time; AttackAim = Move; }
            if (dodge != null && dodge.WasPressedThisFrame()) { dodgePressedAt = Time.time; DodgeAim = Move; }
        }

        /// <summary>True (once) when Attack was pressed within the last <paramref name="buffer"/> seconds.</summary>
        public bool ConsumeAttack(float buffer) => Consume(ref attackPressedAt, buffer);
        public bool ConsumeDodge(float buffer) => Consume(ref dodgePressedAt, buffer);
        public bool AttackBuffered(float buffer) => Time.time - attackPressedAt <= buffer;
        public bool DodgeBuffered(float buffer) => Time.time - dodgePressedAt <= buffer;

        private static bool Consume(ref float pressedAt, float buffer)
        {
            if (Time.time - pressedAt > buffer) return false;
            pressedAt = float.NegativeInfinity;
            return true;
        }

        /// <summary>Ignore action buttons until they are released (used when closing menus).</summary>
        public void SuppressActionsUntilRelease()
        {
            suppressed = true;
            ClearBuffered();
        }

        public void ClearBuffered() => attackPressedAt = dodgePressedAt = float.NegativeInfinity;

        private void OnEnable() { if (instance != null) instance.Enable(); }
        private void OnDisable() { if (instance != null) instance.Disable(); ClearBuffered(); }
        private void OnApplicationFocus(bool focused) { hasFocus = focused; if (!focused) ClearBuffered(); }
        private void OnDestroy() { if (instance != null) Destroy(instance); }
    }
}
