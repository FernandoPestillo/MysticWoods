using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TransparetObject : MonoBehaviour
{
    [Range(0, 1)] [SerializeField] private float _transparencyValue = .6f;
    [SerializeField] private float _transparencyFadeTime = .4f;
    private readonly HashSet<Collider2D> occupants = new();
    private SpriteRenderer spriteRenderer;
    private Coroutine fade;

    private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.GetComponent<PlayerController>() || !occupants.Add(collision)) return;
        FadeTo(_transparencyValue);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (occupants.Remove(collision) && occupants.Count == 0) FadeTo(1);
    }

    private void FadeTo(float target)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Fade(target));
    }

    private IEnumerator Fade(float target)
    {
        float initial = spriteRenderer.color.a;
        float duration = Mathf.Max(.01f, _transparencyFadeTime);
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(initial, target, elapsed / duration));
            yield return null;
        }
        SetAlpha(target);
        fade = null;
    }

    private void SetAlpha(float alpha)
    {
        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }

    private void OnDisable()
    {
        if (fade != null) StopCoroutine(fade);
        fade = null;
        occupants.Clear();
        if (spriteRenderer) SetAlpha(1);
    }
}
