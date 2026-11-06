using MysticWoods.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Compatibility adapter for existing scenes and UnityEvent bindings.
public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenu;
    public GameObject restartMenu;
    private GameMenuController controller;
    private void Awake() => controller = FindAnyObjectByType<GameMenuController>();
    private void Update()
    {
        if (controller != null || Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
        if (restartMenu != null && restartMenu.activeSelf) return;
        if (pauseMenu == null) return;
        pauseMenu.SetActive(!pauseMenu.activeSelf);
        Time.timeScale = pauseMenu.activeSelf ? 0 : 1;
    }
    public void ResumeGame()
    {
        if (controller != null) { controller.Resume(); return; }
        if (pauseMenu != null) pauseMenu.SetActive(false);
        FindAnyObjectByType<PlayerController>()?.SuppressAttackUntilRelease();
        Time.timeScale = 1;
    }
    public void VoltarMenu()
    {
        if (controller != null) { controller.ReturnToMainMenu(); return; }
        Time.timeScale = 1;
        SceneManager.LoadScene("TelaInicial");
    }
}
