using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkeletonController : MonoBehaviour, IDamageable
{
    public float damage = 1f;
    public float knockbackForce = 100f;
    bool isAlive = true;
    public Animator animator;
    public float Health
    {
        set
        {
            if (value < _health)
            {
                animator.SetTrigger("hit");
            }

            _health = value;

            if (_health <= 0)
            {
                animator.SetBool("isAlive", false);
                Targetable = false;
                // Destroy(gameObject);
            }
        }
        get
        {
            return _health;
        }
    }

    public bool Targetable
    {
        get { return _targetable; }
        set
        {
            _targetable = value;

            _skeletonRB2D.simulated = value;
        }
    }

    public float _health = 4;
    public bool _targetable = true;
    public bool causandoDano = false;


    public float _moveSpeedSkeleton = 3.5f;
    private Vector2 _skeletonDirection;
    private Rigidbody2D _skeletonRB2D;

    public DetectionController _detectionArea;

    private SpriteRenderer _spriteRenderer;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        animator.SetBool("isAlive", isAlive);
        _skeletonRB2D = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        _skeletonDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        _spriteRenderer = GetComponent<SpriteRenderer>();

    }

    private void FixedUpdate()
    {
        if (_detectionArea.detectedObjs.Count > 0)
        {
            _skeletonDirection = (_detectionArea.detectedObjs[0].transform.position - transform.position).normalized;

            if (!causandoDano)
            {
                _skeletonRB2D.MovePosition(_skeletonRB2D.position + _skeletonDirection * _moveSpeedSkeleton * Time.fixedDeltaTime);
            }

            if (_skeletonDirection.x > 0)
            {
                _spriteRenderer.flipX = false;
            }
            else if (_skeletonDirection.x < 0)
            {
                _spriteRenderer.flipX = true;
            }
        }
    }


    public void OnHit(float damage, Vector2 knockback)
    {
        Health = Health - damage;

        // Aplicando física ao receber ataque
        _skeletonRB2D.AddForce(knockback);
    }

    public void OnHit(float damage)
    {
        Health = Health - damage;
    }

    public void OnObjectDestroyed()
    {
        Destroy(gameObject);
    }
    public void Revive()
    {
        Debug.Log("Reviver ESqueleto");
        _health = 4f;
        animator.SetBool("isAlive", true);
        Targetable = true;
    }

    // Quando Esqueleto colidir com o Player, aplicar dano e knockback
    void OnCollisionEnter2D(Collision2D collision)
    {
        IDamageable damageable = collision.collider.GetComponent<IDamageable>();

        if (damageable != null && collision.gameObject.CompareTag("Player"))
        {
            causandoDano = true;
            Vector3 parentPosition = gameObject.GetComponentInParent<Transform>().position;
            Vector2 direction = (Vector2)(collision.collider.gameObject.transform.position - transform.position).normalized;
            Vector2 knockback = direction * knockbackForce;

            damageable.OnHit(damage, knockback);
            _skeletonRB2D.AddForce((direction * -1) * knockbackForce, ForceMode2D.Impulse);
            StartCoroutine(DamageRoutine());
        }
    }
    private IEnumerator DamageRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        causandoDano = false;
    }
}
