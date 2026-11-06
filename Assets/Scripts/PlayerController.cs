using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CadencedMovement2D))]
public class PlayerController : MonoBehaviour
{
    public float _playerSpeed = 3.5f;
    public float _playerRunSpeed = 5.25f;
    public HeartSystem _playerHeartSystem;

    [Header("Attack cadence")]
    [SerializeField, Min(.1f)] private float attackDuration = .5f;
    [SerializeField, Min(0f)] private float hitWindowStart = .1f;
    [SerializeField, Min(0f)] private float hitWindowEnd = .3f;
    [SerializeField, Min(0f)] private float inputBuffer = .15f;
    [SerializeField, Range(0f, 1f)] private float attackMovement = .35f;
    [Header("Sword coverage")]
    [SerializeField, Min(.1f)] private float attackReach = 1.3f;
    [SerializeField, Min(.1f)] private float attackWidth = 1.35f;
    [SerializeField] private Vector2 attackCenter = new Vector2(0f, .1f);
    [SerializeField] private SwordHitbox swordRight;
    [SerializeField] private SwordHitbox swordLeft;
    [SerializeField] private SwordHitbox swordUp;
    [SerializeField] private SwordHitbox swordDown;

    private static readonly int AttackState = Animator.StringToHash("Base Layer.Blend Tree Attack");
    private Animator animator;
    private CadencedMovement2D movement;
    private Vector2 inputDirection;
    private Vector2 facing = Vector2.down;
    private bool running;
    private bool suppressAttackUntilRelease = true;
    private bool attackRequested;
    private bool queuedAttack;
    private float attackElapsed;
    private float attackStartedAt;
    private SwordHitbox activeSword;

    public bool IsAttacking { get; private set; }
    public Vector2 Facing => facing;
    private bool Alive => _playerHeartSystem == null ||
        (_playerHeartSystem.Targetable && _playerHeartSystem.Health > 0f);

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<CadencedMovement2D>();
        if (_playerHeartSystem == null) _playerHeartSystem = GetComponent<HeartSystem>();
        swordRight.Configure(Vector2.right, attackReach, attackWidth, attackCenter);
        swordLeft.Configure(Vector2.left, attackReach, attackWidth, attackCenter);
        swordUp.Configure(Vector2.up, attackReach, attackWidth, attackCenter);
        swordDown.Configure(Vector2.down, attackReach, attackWidth, attackCenter);
        SetFacing(facing);
        CloseSwords();
    }

    private void Update()
    {
        if (!Alive) { CancelCombat(); return; }
        if (Time.timeScale <= 0f)
        {
            queuedAttack = attackRequested = false;
            inputDirection = Vector2.zero;
            running = false;
            return;
        }
        var keyboard = Keyboard.current;
        inputDirection = Vector2.zero;
        running = false;
        if (keyboard != null)
        {
            inputDirection.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
            inputDirection.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            running = keyboard.leftShiftKey.isPressed;
        }
        inputDirection = inputDirection.normalized;
        if (suppressAttackUntilRelease)
        {
            attackRequested = queuedAttack = false;
            if (keyboard?.spaceKey.isPressed != true && Mouse.current?.leftButton.isPressed != true)
                suppressAttackUntilRelease = false;
            return;
        }
        bool pressed = keyboard?.spaceKey.wasPressedThisFrame == true ||
            Mouse.current?.leftButton.wasPressedThisFrame == true;
        if (!pressed) return;
        if (!IsAttacking) attackRequested = true;
        else if (attackElapsed >= attackDuration - inputBuffer - .0001f) queuedAttack = true;
    }

    private void FixedUpdate()
    {
        if (!Alive) { CancelCombat(); return; }
        if (attackRequested && !IsAttacking) StartAttack();
        attackRequested = false;
        if (!IsAttacking) SetFacing(CardinalDirection(inputDirection));

        float speed = IsAttacking ? _playerSpeed * attackMovement :
            (running ? _playerRunSpeed : _playerSpeed);
        if (_playerHeartSystem != null && _playerHeartSystem.gettingKnockBack)
            movement.Suspend(Time.fixedDeltaTime);
        if (IsAttacking) movement.LimitSpeed(speed);
        movement.Step(inputDirection * speed, _playerSpeed);

        if (IsAttacking)
        {
            attackElapsed = Time.fixedTime - attackStartedAt;
            if (attackElapsed + .0001f >= hitWindowStart && attackElapsed < hitWindowEnd - .0001f)
            {
                if (!activeSword.IsOpen) activeSword.BeginSwing(transform);
                activeSword.Sample();
            }
            else if (activeSword.IsOpen) activeSword.EndSwing();
            if (attackElapsed + .0001f >= attackDuration)
            {
                CloseSwords();
                IsAttacking = false;
                if (queuedAttack) StartAttack();
            }
        }
        animator.SetInteger("Movimento", IsAttacking ? 2 : (movement.IsMoving ? 1 : 0));
    }

    private void StartAttack()
    {
        CloseSwords();
        queuedAttack = false;
        attackElapsed = 0f;
        attackStartedAt = Time.fixedTime;
        IsAttacking = true;
        SetFacing(CardinalDirection(inputDirection));
        activeSword = facing == Vector2.right ? swordRight : facing == Vector2.left ? swordLeft :
            facing == Vector2.up ? swordUp : swordDown;
        animator.SetFloat("AttackRate", .5f / attackDuration);
        animator.SetInteger("Movimento", 2);
        // Restart queued swings even when the Animator is already in Attack.
        animator.Play(AttackState, 0, 0f);
    }

    private Vector2 CardinalDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) return new Vector2(Mathf.Sign(direction.x), 0);
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x)) return new Vector2(0, Mathf.Sign(direction.y));
        return facing;
    }

    private void SetFacing(Vector2 direction)
    {
        facing = direction;
        animator.SetFloat("AxisX", facing.x);
        animator.SetFloat("AxisY", facing.y);
    }

    private void CloseSwords()
    {
        if (swordRight != null) swordRight.EndSwing();
        if (swordLeft != null) swordLeft.EndSwing();
        if (swordUp != null) swordUp.EndSwing();
        if (swordDown != null) swordDown.EndSwing();
    }

    public void SuppressAttackUntilRelease()
    {
        suppressAttackUntilRelease = Keyboard.current?.spaceKey.isPressed == true ||
            Keyboard.current?.spaceKey.wasPressedThisFrame == true ||
            Mouse.current?.leftButton.isPressed == true ||
            Mouse.current?.leftButton.wasPressedThisFrame == true;
        attackRequested = queuedAttack = false;
    }

    public void CancelCombat()
    {
        IsAttacking = false;
        queuedAttack = attackRequested = false;
        attackElapsed = 0f;
        inputDirection = Vector2.zero;
        CloseSwords();
        if (movement != null) movement.Stop();
        if (animator != null) animator.SetInteger("Movimento", 0);
    }

    private void OnDisable() => CancelCombat();

    private void OnValidate()
    {
        attackDuration = Mathf.Max(.1f, attackDuration);
        hitWindowStart = Mathf.Clamp(hitWindowStart, 0f, attackDuration - .02f);
        hitWindowEnd = Mathf.Clamp(hitWindowEnd, hitWindowStart + .02f, attackDuration);
        inputBuffer = Mathf.Clamp(inputBuffer, 0f, attackDuration);
    }
}
