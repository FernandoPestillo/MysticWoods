using UnityEngine;

/// <summary>Breathes the rendered sprite around its feet without moving physics or hitboxes.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public sealed class IdleSpriteMotion : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float period = 1.8f;
    [SerializeField, Range(0f, .15f)] private float verticalStretch = .025f;
    [SerializeField, Range(0f, .1f)] private float horizontalSquash;

    private static readonly int StretchId = Shader.PropertyToID("_IdleStretch");
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private MaterialPropertyBlock properties;
    private float phase;
    private float weight;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        properties = new MaterialPropertyBlock();
        // Nearby enemies breathe independently, with no random gameplay state changes.
        phase = Mathf.Repeat(transform.position.x * .73f + transform.position.y * .37f, Mathf.PI * 2f);
    }

    private void LateUpdate()
    {
        if (spriteRenderer.sprite == null) return;
        bool idle = animator.isActiveAndEnabled && animator.GetCurrentAnimatorStateInfo(0).IsTag("Idle");
        if (animator.IsInTransition(0))
            idle = idle && animator.GetNextAnimatorStateInfo(0).IsTag("Idle");

        weight = Mathf.MoveTowards(weight, idle ? 1f : 0f, Time.deltaTime / .12f);
        phase = Mathf.Repeat(phase + Time.deltaTime * Mathf.PI * 2f / period, Mathf.PI * 2f);
        float breath = (.5f - .5f * Mathf.Cos(phase)) * weight;
        Bounds bounds = spriteRenderer.sprite.bounds;
        spriteRenderer.GetPropertyBlock(properties);
        properties.SetVector(StretchId, new Vector4(-horizontalSquash * breath,
            verticalStretch * breath, bounds.min.y, bounds.center.x));
        spriteRenderer.SetPropertyBlock(properties);
    }

    private void OnDisable()
    {
        weight = 0f;
        if (spriteRenderer == null || properties == null) return;
        spriteRenderer.GetPropertyBlock(properties);
        properties.SetVector(StretchId, Vector4.zero);
        spriteRenderer.SetPropertyBlock(properties);
    }
}
