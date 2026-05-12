using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class KeysManager : MonoBehaviour
{
    private TMP_Text numeroChavesPrata;
    public int chavesPrata = 0;
    private TMP_Text numeroChavesOuro;
    public int chavesOuro = 0;

    public void EncontrarChavePrata()
    {
        chavesPrata++;
        numeroChavesPrata = GameObject.Find("chavesPrata").GetComponent<TMP_Text>();
        numeroChavesPrata.text = chavesPrata.ToString();
    }
    public void UsarChavePrata()
    {
        chavesPrata--;
        numeroChavesPrata = GameObject.Find("chavesPrata").GetComponent<TMP_Text>();
        numeroChavesPrata.text = chavesPrata.ToString();
    }

    public void EncontrarChaveOuro()
    {
        chavesOuro++;
        numeroChavesOuro = GameObject.Find("chavesOuro").GetComponent<TMP_Text>();
        numeroChavesOuro.text = chavesOuro.ToString();
    }
    public void UsarChaveOuro()
    {
        chavesOuro--;
        numeroChavesOuro = GameObject.Find("chavesOuro").GetComponent<TMP_Text>();
        numeroChavesOuro.text = chavesOuro.ToString();
    }

}
