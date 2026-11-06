using UnityEngine;

/// <summary>Fixed-step locomotion; external impulses retain control during knockback.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public sealed class CadencedMovement2D : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float accelerationTime = .18f;
    [SerializeField, Min(.01f)] private float brakingTime = .10f;
    private Rigidbody2D body;
    private Animator animator;
    private Vector2 velocity;
    private float brakingSpeed;
    private float suspendedUntil;
    public bool IsMoving => velocity.sqrMagnitude > .0025f;
    public Vector2 Velocity => velocity;
    public bool IsSuspended => Time.time < suspendedUntil;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public void Step(Vector2 desiredVelocity, float walkingSpeed)
    {
        if (!body.simulated || Time.time < suspendedUntil)
        {
            Stop();
            return;
        }
        bool braking = desiredVelocity.sqrMagnitude < velocity.sqrMagnitude;
        brakingSpeed = braking ? Mathf.Max(brakingSpeed, velocity.magnitude) : 0f;
        float topSpeed = Mathf.Max(.01f, Mathf.Max(walkingSpeed, Mathf.Max(desiredVelocity.magnitude, brakingSpeed)));
        velocity = Vector2.MoveTowards(velocity, desiredVelocity,
            topSpeed * Time.fixedDeltaTime / (braking ? brakingTime : accelerationTime));
        if (IsMoving) body.MovePosition(body.position + velocity * Time.fixedDeltaTime);
        else velocity = Vector2.zero;
        // This parameter affects locomotion states, never the entire Animator.
        animator.SetFloat("LocomotionRate", Mathf.Clamp(.85f * velocity.magnitude / Mathf.Max(walkingSpeed, .01f), .4f, 1.15f));
    }

    public void LimitSpeed(float speed) => velocity = Vector2.ClampMagnitude(velocity, speed);
    public void Stop()
    {
        velocity = Vector2.zero;
        brakingSpeed = 0f;
    }
    public void Suspend(float seconds)
    {
        suspendedUntil = Mathf.Max(suspendedUntil, Time.time + seconds);
        Stop();
    }

    private void OnDisable()
    {
        Stop();
        suspendedUntil = 0f;
    }
}
