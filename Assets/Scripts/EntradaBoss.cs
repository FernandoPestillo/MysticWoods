using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntradaBoss : MonoBehaviour
{
    public GameObject BarreiraBoss;
    public GameObject BarreiraFinal;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {

            BarreiraBoss.SetActive(true);
            BarreiraFinal.SetActive(true);
            gameObject.SetActive(false);
        }
    }
}
