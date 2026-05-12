using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChestController : MonoBehaviour
{
    public Animator animator;
    public KeysManager KeysManager;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Player tentou abrir baú");
            if (KeysManager.chavesPrata > 0)
            {
                Debug.Log("ABRINDO BAÚ");
                KeysManager.UsarChavePrata();
                KeysManager.EncontrarChaveOuro();
                animator.SetTrigger("Abrir");   
            }
        }
    }
}