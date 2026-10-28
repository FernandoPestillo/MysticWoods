using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MysticWoods.UI
{
    public sealed class MenuSettingsController : MonoBehaviour
    {
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Text volumeValue;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown windowModeDropdown;
        [SerializeField] private Button applyButton;
        [SerializeField] private GameObject confirmationRoot;
        [SerializeField] private TMP_Text confirmationMessage;
        [SerializeField] private Button keepVideoButton;
        [SerializeField] private CanvasGroup formGroup;
        private List<Vector2Int> resolutions;
        private MenuPreferences baseline;
        private MenuPreferences pending;
        private bool isOpen;
        private bool dropdownWasExpanded;
        private double confirmationDeadline;
        public event Action Closed;
        public bool IsConfirmingVideo { get; private set; }
        public int SecondsRemaining => Mathf.Max(0, Mathf.CeilToInt((float)(confirmationDeadline - Time.realtimeSinceStartupAsDouble)));

        private void Awake() => volumeSlider.onValueChanged.AddListener(PreviewVolume);

        public void Open()
        {
            baseline = MainMenuPreferences.Current;
            baseline.Width = Screen.width;
            baseline.Height = Screen.height;
            isOpen = true;
            IsConfirmingVideo = false;
            confirmationRoot.SetActive(false);
            formGroup.interactable = true;
            formGroup.blocksRaycasts = true;
            resolutions = MainMenuPreferences.AvailableResolutions();
            var baselineSize = new Vector2Int(baseline.Width, baseline.Height);
            if (!resolutions.Contains(baselineSize)) resolutions.Add(baselineSize);
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutions.Select(size => $"{size.x} × {size.y}").ToList());
            windowModeDropdown.ClearOptions();
            windowModeDropdown.AddOptions(new List<string> { "Janela", "Tela cheia sem bordas" });
            RefreshControls();
        }

        private void RefreshControls()
        {
            volumeSlider.SetValueWithoutNotify(baseline.Volume);
            volumeValue.text = $"{Mathf.RoundToInt(baseline.Volume * 100)}%";
            resolutionDropdown.SetValueWithoutNotify(resolutions.IndexOf(new Vector2Int(baseline.Width, baseline.Height)));
            windowModeDropdown.SetValueWithoutNotify(baseline.WindowMode == FullScreenMode.Windowed ? 0 : 1);
            resolutionDropdown.RefreshShownValue();
            windowModeDropdown.RefreshShownValue();
        }

        private void PreviewVolume(float volume)
        {
            volumeValue.text = $"{Mathf.RoundToInt(volume * 100)}%";
            if (isOpen && !IsConfirmingVideo) AudioListener.volume = volume;
        }

        public void Apply()
        {
            if (!isOpen || IsConfirmingVideo) return;
            var size = resolutions[resolutionDropdown.value];
            var selected = new MenuPreferences
            {
                Volume = volumeSlider.value, Width = size.x, Height = size.y,
                WindowMode = windowModeDropdown.value == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow
            };
            if (baseline.SameVideo(selected))
            {
                MainMenuPreferences.Commit(selected);
                Finish();
                return;
            }
            pending = selected;
            IsConfirmingVideo = true;
            confirmationDeadline = Time.realtimeSinceStartupAsDouble + 15;
            MainMenuPreferences.ApplyRuntime(pending);
            resolutionDropdown.Hide();
            windowModeDropdown.Hide();
            formGroup.interactable = false;
            formGroup.blocksRaycasts = false;
            confirmationRoot.SetActive(true);
            UpdateConfirmation();
            EventSystem.current.SetSelectedGameObject(keepVideoButton.gameObject);
        }

        public void KeepVideo()
        {
            if (!IsConfirmingVideo) return;
            MainMenuPreferences.Commit(pending);
            IsConfirmingVideo = false;
            confirmationRoot.SetActive(false);
            Finish();
        }

        public void RevertVideo()
        {
            if (!IsConfirmingVideo) return;
            MainMenuPreferences.ApplyRuntime(baseline);
            IsConfirmingVideo = false;
            confirmationRoot.SetActive(false);
            formGroup.interactable = true;
            formGroup.blocksRaycasts = true;
            RefreshControls();
            EventSystem.current.SetSelectedGameObject(applyButton.gameObject);
        }

        public void Cancel()
        {
            if (!isOpen) return;
            MainMenuPreferences.ApplyRuntime(baseline);
            IsConfirmingVideo = false;
            confirmationRoot.SetActive(false);
            Finish();
        }

        public void HandleEscape()
        {
            if (IsConfirmingVideo) { RevertVideo(); return; }
            if (resolutionDropdown.IsExpanded || windowModeDropdown.IsExpanded || dropdownWasExpanded)
            {
                resolutionDropdown.Hide(); windowModeDropdown.Hide(); dropdownWasExpanded = false;
                return;
            }
            Cancel();
        }

        private void Finish()
        {
            isOpen = false;
            resolutionDropdown.Hide(); windowModeDropdown.Hide();
            Closed?.Invoke();
        }

        private void Update()
        {
            if (!IsConfirmingVideo) return;
            if (Time.realtimeSinceStartupAsDouble >= confirmationDeadline) RevertVideo();
            else UpdateConfirmation();
        }

        private void UpdateConfirmation() => confirmationMessage.text =
            $"A configuração anterior será restaurada em <color=#FFD86A>{SecondsRemaining}s</color>.";
        private void LateUpdate() => dropdownWasExpanded = resolutionDropdown.IsExpanded || windowModeDropdown.IsExpanded;

        private void OnDisable()
        {
            if (isOpen) MainMenuPreferences.ApplyRuntime(baseline);
            isOpen = false; IsConfirmingVideo = false;
        }

        private void OnDestroy() => volumeSlider.onValueChanged.RemoveListener(PreviewVolume);
    }
}
