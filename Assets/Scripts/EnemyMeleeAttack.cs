using UnityEngine;

/// <summary>Readable melee attacks. Touching the player never deals contact damage.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CadencedMovement2D), typeof(SpriteRenderer))]
public sealed class EnemyMeleeAttack : MonoBehaviour
{
    public enum AttackPhase { Ready, Windup, Active, Recovery }
    [Header("Attack timing")]
    [SerializeField, Min(.1f)] private float windup = .5f;
    [SerializeField, Min(.02f)] private float activeWindow = .16f;
    [SerializeField, Min(.05f)] private float recovery = .4f;
    [SerializeField, Min(.1f)] private float cooldown = .65f;
    [Header("Reach beyond the body, in world units")]
    [SerializeField, Min(.05f)] private float reach = .45f;
    [SerializeField, Range(30f, 160f)] private float arcAngle = 100f;
    [Header("Preparation visuals")]
    [SerializeField] private Material telegraphMaterial;
    [SerializeField, Range(0f, .2f)] private float squash = .1f;

    private const int Segments = 28;
    private static readonly int StretchId = Shader.PropertyToID("_AttackStretch");
    private static readonly int TintId = Shader.PropertyToID("_AttackTint");
    private readonly Vector3[] vertices = new Vector3[1 + (Segments + 1) * 3];
    private readonly Color[] colors = new Color[1 + (Segments + 1) * 3];
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private CadencedMovement2D movement;
    private SpriteRenderer sprite;
    private Animator animator;
    private IDamageable self;
    private Collider2D victim;
    private MaterialPropertyBlock properties;
    private Mesh mesh;
    private MeshRenderer indicator;
    private Vector2 direction;
    private float phaseStarted;
    private float readyAt;
    private bool attemptedHit;
    private float hitDamage;
    private float hitImpulse;

    public AttackPhase Phase { get; private set; }
    public bool IsBusy => Phase != AttackPhase.Ready;
    public Vector2 AttackDirection => direction;
    public float DamageRadius => Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y) + reach;
    public float WindupDuration => windup;
    public float CooldownDuration => cooldown;
    public bool TelegraphVisible => indicator != null && indicator.enabled;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        movement = GetComponent<CadencedMovement2D>();
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        self = GetComponent<IDamageable>();
        properties = new MaterialPropertyBlock();
        CreateIndicator();
    }

    /// <returns>True while attacking or holding at melee distance, so pursuit stops.</returns>
    public bool Step(Collider2D target, float damage, float impulse)
    {
        if (self == null || !self.Targetable || self.Health <= 0f)
        {
            Cancel();
            return true;
        }
        if (IsBusy)
        {
            if (!ValidTarget(victim)) { Interrupt(); return true; }
            float elapsed = Time.fixedTime - phaseStarted;
            if (Phase == AttackPhase.Windup && elapsed + .0001f >= windup)
                SetPhase(AttackPhase.Active);
            else if (Phase == AttackPhase.Active && elapsed + .0001f >= activeWindow)
                SetPhase(AttackPhase.Recovery);
            else if (Phase == AttackPhase.Recovery && elapsed + .0001f >= recovery)
            {
                SetPhase(AttackPhase.Ready);
                readyAt = Time.fixedTime + cooldown;
                victim = null;
            }
            if (Phase == AttackPhase.Active && !attemptedHit && InsideAttack(victim))
            {
                attemptedHit = true;
                victim.GetComponentInParent<IDamageable>().OnHit(hitDamage, direction * hitImpulse);
            }
            return true;
        }
        if (!ValidTarget(target)) return false;
        bool close = bodyCollider.Distance(target).distance <= reach;
        if (!close) return false;
        if (Time.fixedTime < readyAt || movement.IsSuspended) return true;

        victim = target;
        direction = ((Vector2)target.bounds.center - (Vector2)bodyCollider.bounds.center).normalized;
        if (direction == Vector2.zero) direction = sprite.flipX ? Vector2.left : Vector2.right;
        if (Mathf.Abs(direction.x) > .01f) sprite.flipX = direction.x < 0f;
        hitDamage = damage;
        hitImpulse = impulse;
        attemptedHit = false;
        movement.Stop();
        body.linearVelocity = Vector2.zero;
        animator.SetBool("isMoving", false);
        SetPhase(AttackPhase.Windup);
        return true;
    }

    private bool ValidTarget(Collider2D target)
    {
        if (target == null || !target.enabled || target.isTrigger || !target.gameObject.activeInHierarchy) return false;
        var damageable = target.GetComponentInParent<IDamageable>();
        return damageable != null && damageable != self && damageable.Targetable && damageable.Health > 0f;
    }

    private bool InsideAttack(Collider2D target)
    {
        Vector2 origin = bodyCollider.bounds.center;
        Vector2 closest = target.ClosestPoint(origin);
        Vector2 toTarget = closest - origin;
        if (toTarget.sqrMagnitude > DamageRadius * DamageRadius) return false;
        // At contact, use body centers to avoid treating a rear overlap as a front hit.
        Vector2 angleDirection = (Vector2)target.bounds.center - origin;
        return Vector2.Angle(direction, angleDirection) <= arcAngle * .5f;
    }

    private void SetPhase(AttackPhase phase)
    {
        Phase = phase;
        phaseStarted = Time.fixedTime;
        animator.SetBool("isAttacking", IsBusy);
        if (indicator != null) indicator.enabled = phase == AttackPhase.Windup || phase == AttackPhase.Active;
        if (phase == AttackPhase.Ready || phase == AttackPhase.Recovery) ClearSpriteEffect();
    }

    public void Interrupt()
    {
        if (animator == null) return;
        Cancel();
        readyAt = Mathf.Max(readyAt, Time.fixedTime + cooldown);
    }

    public void Cancel()
    {
        Phase = AttackPhase.Ready;
        victim = null;
        attemptedHit = false;
        if (animator != null) animator.SetBool("isAttacking", false);
        if (indicator != null) indicator.enabled = false;
        ClearSpriteEffect();
    }

    private void Update()
    {
        float elapsed = Time.time - phaseStarted;
        float progress = Phase == AttackPhase.Windup ? .34f * Mathf.Clamp01(elapsed / windup) :
            Phase == AttackPhase.Active ? Mathf.Lerp(.5f, .84f, Mathf.Clamp01(elapsed / activeWindow)) :
            Phase == AttackPhase.Recovery ? Mathf.Lerp(.84f, 1f, Mathf.Clamp01(elapsed / recovery)) : 0f;
        animator.SetFloat("MeleeProgress", progress);
    }

    private void LateUpdate()
    {
        if (!IsBusy || sprite.sprite == null) return;
        float progress = Mathf.Clamp01((Time.time - phaseStarted) / windup);
        float amount = Phase == AttackPhase.Windup ? Mathf.SmoothStep(0f, 1f, progress) :
            Phase == AttackPhase.Active ? -.45f : 0f;
        var bounds = sprite.sprite.bounds;
        sprite.GetPropertyBlock(properties);
        properties.SetVector(StretchId, new Vector4(squash * .65f * amount, -squash * amount, bounds.min.y, bounds.center.x));
        properties.SetColor(TintId, Phase == AttackPhase.Windup ? new Color(1f, .62f, .12f, .1f + progress * .2f) :
            Phase == AttackPhase.Active ? new Color(1f, .25f, .1f, .15f) : Color.clear);
        sprite.SetPropertyBlock(properties);
        if (indicator.enabled) UpdateIndicator(progress);
    }

    private void CreateIndicator()
    {
        var child = new GameObject("Attack telegraph");
        child.transform.SetParent(transform, false);
        var filter = child.AddComponent<MeshFilter>();
        indicator = child.AddComponent<MeshRenderer>();
        indicator.sharedMaterial = telegraphMaterial;
        indicator.sortingLayerID = sprite.sortingLayerID;
        indicator.sortingOrder = sprite.sortingOrder - 1;
        indicator.enabled = false;
        mesh = new Mesh { name = "Enemy melee warning sector" };
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;
        var triangles = new int[Segments * 9];
        int borderStart = Segments + 2;
        for (int i = 0; i < Segments; i++)
        {
            int t = i * 9, a = borderStart + i * 2;
            triangles[t] = 0; triangles[t + 1] = i + 1; triangles[t + 2] = i + 2;
            triangles[t + 3] = a; triangles[t + 4] = a + 1; triangles[t + 5] = a + 2;
            triangles[t + 6] = a + 1; triangles[t + 7] = a + 3; triangles[t + 8] = a + 2;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
    }

    private void UpdateIndicator(float progress)
    {
        Vector2 origin = bodyCollider.bounds.center;
        float radius = DamageRadius;
        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Color tint = Phase == AttackPhase.Active ? new Color(1f, .22f, .1f) : new Color(1f, .7f, .16f);
        vertices[0] = transform.InverseTransformPoint(origin);
        colors[0] = new Color(tint.r, tint.g, tint.b, .04f);
        for (int i = 0; i <= Segments; i++)
        {
            float angle = (baseAngle - arcAngle * .5f + arcAngle * i / Segments) * Mathf.Deg2Rad;
            Vector2 ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            int border = Segments + 2 + i * 2;
            vertices[i + 1] = transform.InverseTransformPoint(origin + ray * radius);
            vertices[border] = vertices[i + 1];
            vertices[border + 1] = transform.InverseTransformPoint(origin + ray * (radius - .035f));
            colors[i + 1] = new Color(tint.r, tint.g, tint.b, .1f + progress * .15f);
            colors[border] = colors[border + 1] = new Color(tint.r, tint.g, tint.b, .75f + progress * .25f);
        }
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }

    private void ClearSpriteEffect()
    {
        if (sprite == null || properties == null) return;
        sprite.GetPropertyBlock(properties);
        properties.SetVector(StretchId, Vector4.zero);
        properties.SetColor(TintId, Color.clear);
        sprite.SetPropertyBlock(properties);
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    private void OnValidate()
    {
        windup = Mathf.Max(.1f, windup);
        activeWindow = Mathf.Max(.02f, activeWindow);
        recovery = Mathf.Max(.05f, recovery);
        cooldown = Mathf.Max(.1f, cooldown);
        reach = Mathf.Max(.05f, reach);
    }
}
