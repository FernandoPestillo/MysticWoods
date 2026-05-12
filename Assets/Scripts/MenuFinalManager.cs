using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuFinalManager : MonoBehaviour
{
    [SerializeField] private string voltarMenuPrincipal;

    public void VoltarMenu()
    {
        SceneManager.LoadScene(voltarMenuPrincipal);
    }

    public void SairJogo()
    {
        Debug.Log("Sair do Jogo");
        Application.Quit();
    }
}
