using System.Collections;
using MysticWoods.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuFinalManager : MonoBehaviour
{
    [SerializeField] private string voltarMenuPrincipal = "TelaInicial";
    [SerializeField] private string nomeLevelJogo = "FaseMysticWoods";
    [SerializeField] private Button jogarNovamenteButton;
    private bool transitioning;
    private void Awake()
    {
        Time.timeScale = 1;
        MainMenuPreferences.LoadAndApply();
    }
    private IEnumerator Start()
    {
        yield return null;
        if (jogarNovamenteButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(jogarNovamenteButton.gameObject);
    }
    public void JogarNovamente() => Load(nomeLevelJogo);
    public void VoltarMenu() => Load(voltarMenuPrincipal);
    private void Load(string scene)
    {
        if (transitioning) return;
        transitioning = true;
        Time.timeScale = 1;
        SceneManager.LoadScene(scene);
    }
    public void SairJogo()
    {
        Time.timeScale = 1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
