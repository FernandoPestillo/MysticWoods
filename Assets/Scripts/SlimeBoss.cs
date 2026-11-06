using UnityEngine;

[RequireComponent(typeof(CadencedMovement2D), typeof(EnemyMeleeAttack))]
public class SlimeBoss : MonoBehaviour, IDamageable
{
    public float damage = 1f;
    public float knockbackForce = 12f;
    public Animator animator;
    public float _health = 20f;
    public bool _targetable = true;
    public bool causandoDano;
    public float _moveSpeedSlime = 1.7f;
    public DetectionController _detectionArea;
    public GameObject BarreiraFinal;

    private Rigidbody2D _slimeRB2D;
    private SpriteRenderer spriteRenderer;
    private CadencedMovement2D movement;
    private EnemyMeleeAttack melee;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        _slimeRB2D = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<CadencedMovement2D>();
        melee = GetComponent<EnemyMeleeAttack>();
    }

    private void Start() => animator.SetBool("isAlive", _health > 0f);

    public float Health
    {
        get => _health;
        set
        {
            if (value < _health && _health > 0f) animator.SetTrigger("hit");
            _health = value;
            if (_health > 0f) return;
            animator.SetBool("isAlive", false);
            Targetable = false;
            if (BarreiraFinal != null) BarreiraFinal.SetActive(false);
        }
    }

    public bool Targetable
    {
        get => _targetable;
        set
        {
            _targetable = value;
            _slimeRB2D.simulated = value;
            if (!value)
            {
                movement.Stop();
                melee.Cancel();
                causandoDano = false;
                animator.SetBool("isMoving", false);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!Targetable || _health <= 0f) return;
        Collider2D target = _detectionArea != null ? _detectionArea.Target : null;
        Vector2 direction = target != null ? ((Vector2)target.bounds.center - _slimeRB2D.position).normalized : Vector2.zero;
        bool holdPosition = melee.Step(target, damage, knockbackForce);
        causandoDano = melee.IsBusy;
        movement.Step(holdPosition ? Vector2.zero : direction * _moveSpeedSlime, _moveSpeedSlime);
        animator.SetBool("isMoving", movement.IsMoving);
        if (!melee.IsBusy && Mathf.Abs(direction.x) > .01f) spriteRenderer.flipX = direction.x < 0f;
    }

    public void OnHit(float amount, Vector2 knockback)
    {
        if (!Targetable) return;
        OnHit(amount);
        if (Targetable) _slimeRB2D.AddForce(knockback);
    }

    public void OnHit(float amount)
    {
        if (!Targetable) return;
        melee.Interrupt();
        causandoDano = false;
        Health -= amount;
        // Let the existing physics impulse act before locomotion takes over again.
        movement.Suspend(.12f);
    }

    public void OnObjectDestroyed() => Destroy(gameObject);

    private void OnDisable()
    {
        if (melee != null) melee.Cancel();
        causandoDano = false;
        if (movement != null) movement.Stop();
    }
}
