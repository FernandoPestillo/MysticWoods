using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class SwordHitbox : MonoBehaviour
{
    public float _swordDamage = 1f;
    public float knockbackForce = 600f;
    public Collider2D swordCollider;
    private BoxCollider2D box;
    private Transform owner;
    private readonly HashSet<IDamageable> struck = new HashSet<IDamageable>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private ContactFilter2D filter;
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        swordCollider = box;
        box.isTrigger = true;
        box.enabled = false;
        filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.AllLayers);
    }

    public void Configure(Vector2 direction, float reach, float width, Vector2 center)
    {
        if (box == null) box = GetComponent<BoxCollider2D>();
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        box.offset = center + direction * (reach * .5f);
        box.size = Mathf.Abs(direction.x) > 0 ? new Vector2(reach, width) : new Vector2(width, reach);
        box.isTrigger = true;
        box.enabled = false;
    }

    public void BeginSwing(Transform attacker)
    {
        owner = attacker;
        struck.Clear();
        IsOpen = true;
        box.enabled = true;
    }

    public void Sample()
    {
        if (!IsOpen || owner == null) return;
        Vector3 scale = transform.lossyScale;
        Vector2 size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        // Explicit queries also hit enemies already overlapping when the window opens.
        Physics2D.OverlapBox(transform.TransformPoint(box.offset), size, transform.eulerAngles.z, filter, overlaps);
        foreach (var other in overlaps)
        {
            if (other == null || other.isTrigger || other.transform.IsChildOf(owner)) continue;
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null || !target.Targetable || target.Health <= 0 || !struck.Add(target)) continue;
            Vector2 direction = ((Vector2)other.bounds.center - (Vector2)owner.position).normalized;
            if (direction == Vector2.zero) direction = ((Vector2)box.bounds.center - (Vector2)owner.position).normalized;
            target.OnHit(_swordDamage, direction * knockbackForce);
        }
    }

    public void EndSwing()
    {
        IsOpen = false;
        if (box == null) box = GetComponent<BoxCollider2D>();
        box.enabled = false;
        struck.Clear();
        owner = null;
    }

    private void OnDisable() => EndSwing();

    private void OnDrawGizmosSelected()
    {
        var collider = GetComponent<BoxCollider2D>();
        Gizmos.color = IsOpen ? Color.red : Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(collider.offset, collider.size);
    }
}
