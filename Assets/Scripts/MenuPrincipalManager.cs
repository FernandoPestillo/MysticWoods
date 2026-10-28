using System.Collections;
using MysticWoods.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuPrincipalManager : MonoBehaviour
{
    [SerializeField] private string nomeLevelJogo = "FaseMysticWoods";
    [SerializeField] private GameObject painelMenuInicial;
    [SerializeField] private GameObject painelComandos;
    [SerializeField] private GameObject painelConfiguracoes;
    [SerializeField] private GameObject modalOverlay;
    [SerializeField] private MenuSettingsController settingsController;
    [SerializeField] private Button jogarButton;
    [SerializeField] private Button comandosButton;
    [SerializeField] private Button configuracoesButton;
    [SerializeField] private Button voltarComandosButton;
    [SerializeField] private Slider volumeSlider;
    private Coroutine selectionRoutine;
    private bool startingGame;

    private void Awake()
    {
        Time.timeScale = 1;
        MainMenuPreferences.LoadAndApply();
    }

    private void Start()
    {
        settingsController.Closed += OnSettingsClosed;
        painelComandos.SetActive(false);
        painelConfiguracoes.SetActive(false);
        modalOverlay.SetActive(false);
        painelMenuInicial.SetActive(true);
        Select(jogarButton.gameObject);
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
        if (painelConfiguracoes.activeSelf) settingsController.HandleEscape();
        else if (painelComandos.activeSelf) FecharComandos();
    }

    public void Jogar()
    {
        if (startingGame) return;
        startingGame = true;
        Time.timeScale = 1;
        SceneManager.LoadScene(nomeLevelJogo);
    }
    public void AbrirComandos()
    {
        painelMenuInicial.SetActive(false);
        modalOverlay.SetActive(true);
        painelComandos.SetActive(true);
        Select(voltarComandosButton.gameObject);
    }
    public void FecharComandos()
    {
        painelComandos.SetActive(false);
        modalOverlay.SetActive(false);
        painelMenuInicial.SetActive(true);
        Select(comandosButton.gameObject);
    }

    public void AbrirConfiguracoes()
    {
        painelMenuInicial.SetActive(false);
        modalOverlay.SetActive(true);
        painelConfiguracoes.SetActive(true);
        settingsController.Open();
        Select(volumeSlider.gameObject);
    }

    public void FecharConfiguracoes() => settingsController.Cancel();

    private void OnSettingsClosed()
    {
        painelConfiguracoes.SetActive(false);
        modalOverlay.SetActive(false);
        painelMenuInicial.SetActive(true);
        Select(configuracoesButton.gameObject);
    }

    private void Select(GameObject target)
    {
        if (selectionRoutine != null) StopCoroutine(selectionRoutine);
        selectionRoutine = StartCoroutine(SelectAfterFrame(target));
    }

    private IEnumerator SelectAfterFrame(GameObject target)
    {
        yield return null;
        if (target != null && target.activeInHierarchy && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
        selectionRoutine = null;
    }
    public void SairJogo()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (settingsController != null) settingsController.Closed -= OnSettingsClosed;
    }
}
