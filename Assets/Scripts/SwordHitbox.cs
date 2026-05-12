using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    public float _swordDamage = 1f;
    public float knockbackForce = 4000f;
    public Collider2D swordCollider;

    void Start()
    {
        if(swordCollider == null)
        {
            Debug.LogWarning("swordCollider nao encontrado");
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {


        IDamageable damageableObject = col.collider.GetComponent<IDamageable>();
        if(damageableObject != null)
        {
            Vector3 parentPosition = gameObject.GetComponentInParent<Transform>().position;
            Vector2 direction  = (Vector2) (col.collider.gameObject.transform.position - parentPosition).normalized;
            Vector2 knockback = direction * knockbackForce;

            // col.collider.SendMessage("OnHit", _swordDamage, knockback);
            damageableObject.OnHit(_swordDamage, knockback);
        } else {
            Debug.LogWarning("Objeto não implementou inferface IDamageable");
        }

    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
