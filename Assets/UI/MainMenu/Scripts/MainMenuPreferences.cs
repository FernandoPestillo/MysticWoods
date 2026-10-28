using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MysticWoods.UI
{
    [Serializable]
    public struct MenuPreferences
    {
        public float Volume;
        public int Width;
        public int Height;
        public FullScreenMode WindowMode;
        public bool SameVideo(MenuPreferences other) => Width == other.Width
            && Height == other.Height && WindowMode == other.WindowMode;
    }

    public static class MainMenuPreferences
    {
        public const string VolumeKey = "MysticWoods.Menu.MasterVolume";
        public const string WidthKey = "MysticWoods.Menu.Width";
        public const string HeightKey = "MysticWoods.Menu.Height";
        public const string ModeKey = "MysticWoods.Menu.WindowMode";
        public static MenuPreferences Current { get; private set; }

        public static List<Vector2Int> AvailableResolutions()
        {
            var values = new HashSet<Vector2Int>();
            foreach (var resolution in Screen.resolutions)
                if (resolution.width >= 960 && resolution.height >= 540)
                    values.Add(new Vector2Int(resolution.width, resolution.height));
            values.Add(new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height)));
            return values.OrderByDescending(value => value.x).ThenByDescending(value => value.y).ToList();
        }

        public static MenuPreferences LoadAndApply()
        {
            var preferences = new MenuPreferences
            {
                Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1)),
                Width = Screen.width,
                Height = Screen.height,
                WindowMode = Screen.fullScreenMode == FullScreenMode.Windowed
                    ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow
            };
            if (float.IsNaN(preferences.Volume)) preferences.Volume = 1;
            var storedSize = new Vector2Int(PlayerPrefs.GetInt(WidthKey, Screen.width),
                PlayerPrefs.GetInt(HeightKey, Screen.height));
            if (AvailableResolutions().Contains(storedSize))
            {
                preferences.Width = storedSize.x;
                preferences.Height = storedSize.y;
            }
            int storedMode = PlayerPrefs.GetInt(ModeKey, (int)preferences.WindowMode);
            if (storedMode == (int)FullScreenMode.Windowed || storedMode == (int)FullScreenMode.FullScreenWindow)
                preferences.WindowMode = (FullScreenMode)storedMode;
            Current = preferences;
            ApplyRuntime(preferences);
            return preferences;
        }

        public static void ApplyRuntime(MenuPreferences preferences)
        {
            AudioListener.volume = Mathf.Clamp01(preferences.Volume);
            if (Screen.width != preferences.Width || Screen.height != preferences.Height
                || Screen.fullScreenMode != preferences.WindowMode)
                Screen.SetResolution(preferences.Width, preferences.Height, preferences.WindowMode);
        }

        public static void Commit(MenuPreferences preferences)
        {
            ApplyRuntime(preferences);
            PlayerPrefs.SetFloat(VolumeKey, preferences.Volume);
            PlayerPrefs.SetInt(WidthKey, preferences.Width);
            PlayerPrefs.SetInt(HeightKey, preferences.Height);
            PlayerPrefs.SetInt(ModeKey, (int)preferences.WindowMode);
            PlayerPrefs.Save();
            Current = preferences;
        }
    }
}
