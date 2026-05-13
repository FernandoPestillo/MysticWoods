using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartMenu : MonoBehaviour
{

    public void Restart()
    {
        SceneManager.LoadScene("FaseMysticWoods");
    }

    public void VoltarMenu()
    {
        SceneManager.LoadScene("TelaInicial");
    }
}
