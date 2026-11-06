using System.Collections;
using UnityEngine;
using UnityEngine.UI;


public class HeartSystem : MonoBehaviour, IDamageable
{
    public GameObject MenuRestart;
    public event System.Action Died;
    [Header("Damage response")]
    [SerializeField, Min(0f)] private float hurtInvulnerability = .35f;
    [SerializeField, Min(.02f)] private float knockbackDuration = .18f;
    [SerializeField, Min(0f)] private float maxKnockbackImpulse = 12f;
    private float nextDamageAt;
    private Coroutine knockRoutine;
    public bool IsInvulnerable => Time.time < nextDamageAt;
    public float Health
    {
        set
        {
            bool wasAlive = vida > 0f;
            vida = value;

            if (vida <= 0 && wasAlive)
            {
                GetComponent<PlayerController>()?.CancelCombat();
                animator.SetBool("isAlive", false);
                Targetable = false;
                if (Died != null) Died.Invoke();
                else if (MenuRestart != null) MenuRestart.SetActive(true);
            }
        }
        get
        {
            return vida;
        }
    }

    private const float NormalHealth = 6f;
    public float vida = NormalHealth;
    public float vidaMaxima = NormalHealth;
    bool isAlive = true;
    public Animator animator;
    private Rigidbody2D _playerRB2D;

    public Image[] coracao;
    public Sprite cheio;
    public Sprite vazio;

    

    public bool _targetable = true;
    public bool gettingKnockBack = false;

    public bool Targetable
    {
        get { return _targetable; }
        set
        {
            _targetable = value;

            _playerRB2D.simulated = value;
        }
    }
    private void Awake()
    {
        animator = GetComponent<Animator>();
        _playerRB2D = GetComponent<Rigidbody2D>();
    }

    // Start is called before the first frame update
    void Start()
    {
        Time.timeScale = 1;
        vidaMaxima = NormalHealth;
        vida = NormalHealth;
        animator.SetBool("isAlive", isAlive);
    }

    // Update is called once per frame
    void Update()
    {
        HealthLogic();  
    }
    void HealthLogic()
    {
        if(vida > vidaMaxima) 
        {
            vida = vidaMaxima;
        }

        for (int i = 0; i < coracao.Length; i++)
        {
            if (i< vida)
            {
                coracao[i].sprite = cheio;
            }
            else
            {
                coracao[i].sprite = vazio;
            }

            if (i < vidaMaxima)
            {
                coracao[i].enabled = true;
            }
            else
            {
                coracao[i].enabled = false;
            }
        }
    }

    public void OnHit(float damage, Vector2 knockback)
    {
        if (!Targetable || IsInvulnerable || damage <= 0f) return;
        nextDamageAt = Time.time + hurtInvulnerability;
        Health -= damage;
        if (!Targetable) { gettingKnockBack = false; return; }
        if (knockRoutine != null) StopCoroutine(knockRoutine);
        gettingKnockBack = true;
        GetComponent<CadencedMovement2D>()?.Suspend(knockbackDuration);
        _playerRB2D.linearVelocity = Vector2.zero;
        _playerRB2D.AddForce(Vector2.ClampMagnitude(knockback, maxKnockbackImpulse), ForceMode2D.Impulse);
        knockRoutine = StartCoroutine(KnockRoutine());
    }

    private IEnumerator KnockRoutine()
    {
        yield return new WaitForSeconds(knockbackDuration);
        gettingKnockBack = false;
        knockRoutine = null;
    }

    public void OnHit(float damage) => OnHit(damage, Vector2.zero);

    private void OnDisable()
    {
        StopAllCoroutines();
        gettingKnockBack = false;
        knockRoutine = null;
        nextDamageAt = 0f;
    }

    public void OnObjectDestroyed()
    {
        throw new System.NotImplementedException();
    }

}
