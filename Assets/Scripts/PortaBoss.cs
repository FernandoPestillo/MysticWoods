using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortaBoss : MonoBehaviour
{
    public KeysManager KeysManager;
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Player tentou abrir PORTA");
            if (KeysManager.chavesOuro > 0)
            {
                Debug.Log("Liberou BOSS");
                KeysManager.UsarChaveOuro();
                gameObject.SetActive(false);
            }
        }
    }
}
