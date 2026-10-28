using System.Collections;
using UnityEngine;
using UnityEngine.UI;


public class HeartSystem : MonoBehaviour, IDamageable
{
    public GameObject MenuRestart;
    public float Health
    {
        set
        {
            vida = value;

            if (vida <= 0)
            {
                animator.SetBool("isAlive", false);
                Targetable = false;
                MenuRestart.SetActive(true);
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
    // Start is called before the first frame update
    void Start()
    {
        Time.timeScale = 1;
        vidaMaxima = NormalHealth;
        vida = NormalHealth;
        animator = GetComponent<Animator>();
        animator.SetBool("isAlive", isAlive);
        _playerRB2D = GetComponent<Rigidbody2D>();
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
        gettingKnockBack = true;
        Health = Health - damage;
        _playerRB2D.AddForce(knockback, ForceMode2D.Impulse);
        StartCoroutine(KnockRoutine());
    }
    private IEnumerator KnockRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        gettingKnockBack = false;
    }

    public void OnHit(float damage)
    {
        Health = Health - damage;
    }

    public void OnObjectDestroyed()
    {
        throw new System.NotImplementedException();
    }

}
