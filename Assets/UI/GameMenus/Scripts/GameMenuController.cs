using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MysticWoods.UI
{
    public sealed class GameMenuController : MonoBehaviour
    {
        public enum MenuState { Playing, Paused, Settings, Commands, Dying, GameOver, Transitioning }
        [SerializeField] private GameObject overlay;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject settingsRoot;
        [SerializeField] private MenuSettingsController settings;
        [SerializeField] private MenuCommandsController commands;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button commandsButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private AnimationClip deathClip;
        [SerializeField] private string levelScene = "FaseMysticWoods";
        [SerializeField] private string mainMenuScene = "TelaInicial";
        private PlayerController player;
        private HeartSystem heart;
        private Coroutine selection;
        private Coroutine deathRoutine;
        public MenuState State { get; private set; }
        public GameObject PausePanel => pausePanel;
        public GameObject GameOverPanel => gameOverPanel;
        public MenuSettingsController Settings => settings;
        public MenuCommandsController Commands => commands;

        private void Awake()
        {
            Time.timeScale = 1;
            MainMenuPreferences.LoadAndApply();
            player = FindAnyObjectByType<PlayerController>();
            heart = player != null ? player.GetComponent<HeartSystem>() : null;
            if (heart != null) heart.Died += BeginDying;
            settings.Closed += SettingsClosed;
            commands.Closed += CommandsClosed;
            HidePanels();
            overlay.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            switch (State)
            {
                case MenuState.Playing: Pause(); break;
                case MenuState.Paused: Resume(); break;
                case MenuState.Settings: settings.HandleEscape(); break;
                case MenuState.Commands: CommandsClosed(); break;
            }
        }

        public void Pause()
        {
            if (State != MenuState.Playing || (heart != null && heart.Health <= 0)) return;
            State = MenuState.Paused;
            Time.timeScale = 0;
            overlay.SetActive(true);
            pausePanel.SetActive(true);
            Select(resumeButton.gameObject);
        }

        public void Resume()
        {
            if (State != MenuState.Paused) return;
            State = MenuState.Playing;
            HidePanels();
            overlay.SetActive(false);
            ClearSelection();
            if (player != null) player.SuppressAttackUntilRelease();
            Time.timeScale = 1;
        }

        public void OpenSettings()
        {
            if (State != MenuState.Paused) return;
            State = MenuState.Settings;
            pausePanel.SetActive(false);
            settingsRoot.SetActive(true);
            settings.Open();
            Select(settings.InitialSelection.gameObject);
        }

        public void OpenCommands()
        {
            if (State != MenuState.Paused) return;
            State = MenuState.Commands;
            pausePanel.SetActive(false);
            commands.gameObject.SetActive(true);
            Select(commands.BackButton.gameObject);
        }

        private void SettingsClosed()
        {
            settingsRoot.SetActive(false);
            if (State != MenuState.Settings) return;
            State = MenuState.Paused;
            pausePanel.SetActive(true);
            Select(settingsButton.gameObject);
        }

        private void CommandsClosed()
        {
            commands.gameObject.SetActive(false);
            if (State != MenuState.Commands) return;
            State = MenuState.Paused;
            pausePanel.SetActive(true);
            Select(commandsButton.gameObject);
        }

        public void BeginDying()
        {
            if (State == MenuState.Dying || State == MenuState.GameOver || State == MenuState.Transitioning) return;
            State = MenuState.Dying;
            ClearSelection();
            settings.Cancel();
            HidePanels();
            overlay.SetActive(false);
            // The character's death animation runs before the world is frozen.
            Time.timeScale = 1;
            deathRoutine = StartCoroutine(ShowDeathAfterAnimation());
        }

        private IEnumerator ShowDeathAfterAnimation()
        {
            yield return null;
            yield return new WaitForSeconds(deathClip != null ? deathClip.length : .375f);
            deathRoutine = null;
            if (State != MenuState.Dying) yield break;
            State = MenuState.GameOver;
            Time.timeScale = 0;
            overlay.SetActive(true);
            gameOverPanel.SetActive(true);
            Select(retryButton.gameObject);
        }

        public void RestartLevel() => Transition(levelScene);
        public void ReturnToMainMenu() => Transition(mainMenuScene);

        private void Transition(string scene)
        {
            if (State == MenuState.Transitioning) return;
            State = MenuState.Transitioning;
            if (deathRoutine != null) StopCoroutine(deathRoutine);
            deathRoutine = null;
            ClearSelection();
            settings.Cancel();
            if (player != null) player.SuppressAttackUntilRelease();
            Time.timeScale = 1;
            SceneManager.LoadScene(scene);
        }

        private void HidePanels()
        {
            pausePanel.SetActive(false);
            gameOverPanel.SetActive(false);
            settingsRoot.SetActive(false);
            commands.gameObject.SetActive(false);
        }

        private void Select(GameObject target)
        {
            ClearSelection();
            selection = StartCoroutine(SelectAfterFrame(target));
        }

        private IEnumerator SelectAfterFrame(GameObject target)
        {
            yield return null;
            if (target != null && target.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target);
            selection = null;
        }

        private void ClearSelection()
        {
            if (selection != null) StopCoroutine(selection);
            selection = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            if (heart != null) heart.Died -= BeginDying;
            if (settings != null) settings.Closed -= SettingsClosed;
            if (commands != null) commands.Closed -= CommandsClosed;
            if (State != MenuState.Playing) Time.timeScale = 1;
        }
    }
}
