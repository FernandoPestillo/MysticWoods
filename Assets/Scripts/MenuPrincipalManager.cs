using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuPrincipalManager : MonoBehaviour
{
    [SerializeField] private string nomeLevelJogo;
    [SerializeField] private GameObject painelMenuInicial;
    [SerializeField] private GameObject painelComandos;

    public void Jogar()
    {
        SceneManager.LoadScene(nomeLevelJogo);
        Time.timeScale = 0;
    }
    public void AbrirComandos()
    {
        painelMenuInicial.SetActive(false);
        painelComandos.SetActive(true);
    }
    public void FecharComandos()
    {
        painelMenuInicial.SetActive(true);
        painelComandos.SetActive(false);
    }
    public void SairJogo()
    {
        Debug.Log("Fechando o Jogo");
        Application.Quit();
    }

}
