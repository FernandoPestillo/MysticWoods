using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sword_attack : MonoBehaviour
{
    Vector2 attackOffset;
    Collider2D swordCollider;

    private void Start()
    {
        swordCollider = GetComponent<Collider2D>();
    }

    public void AttackRight()
    {
        swordCollider.enabled = true;
    }
    public void AttackLeft()
    {
        swordCollider.enabled = true;
    }
    public void StopAttack()
    {
        swordCollider.enabled = false;
    }
}
