using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyController : MonoBehaviour
{
    public KeysManager KeysManager;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Encontrou Chave!");
            Destroy(gameObject);
            KeysManager.EncontrarChavePrata();
        }
    }
}
