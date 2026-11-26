using MysticWoods.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RestartMenu : MonoBehaviour
{
    public void Restart()
    {
        var controller = FindAnyObjectByType<GameMenuController>();
        if (controller != null) { controller.RestartLevel(); return; }
        Time.timeScale = 1;
        SceneManager.LoadScene("FaseMysticWoods");
    }
    public void VoltarMenu()
    {
        var controller = FindAnyObjectByType<GameMenuController>();
        if (controller != null) { controller.ReturnToMainMenu(); return; }
        Time.timeScale = 1;
        SceneManager.LoadScene("TelaInicial");
    }
}
