using System;
using System.IO;
using System.Windows;

namespace FolioDesk.Services;

public static class LocalizationService
{
    private static readonly string[] _languages = ["ko", "en", "zh", "ja"];
    private static int _currentIndex = 0;

    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FolioDesk", "language.cfg");

    public static string CurrentLang => _languages[_currentIndex];

    public static void Initialize()
    {
        var lang = "ko";
        if (File.Exists(_settingsPath))
            lang = File.ReadAllText(_settingsPath).Trim();

        var idx = Array.IndexOf(_languages, lang);
        _currentIndex = idx >= 0 ? idx : 0;
        ApplyLanguage(CurrentLang, persist: false);
    }

    public static void ToggleLanguage()
    {
        _currentIndex = (_currentIndex + 1) % _languages.Length;
        ApplyLanguage(CurrentLang, persist: true);
    }

    public static void SetLanguage(string lang)
    {
        var idx = Array.IndexOf(_languages, lang);
        _currentIndex = idx >= 0 ? idx : 0;
        ApplyLanguage(CurrentLang, persist: true);
    }

    private static void ApplyLanguage(string lang, bool persist)
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Strings/{lang}.xaml", UriKind.Absolute)
        };

        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Source?.ToString().Contains("/Resources/Strings/") == true)
            {
                merged.RemoveAt(i);
                break;
            }
        }
        merged.Add(dict);

        if (!persist) return;

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, lang);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Warning($"Failed to save language setting '{lang}': {ex.Message}");
        }
    }

    public static string Get(string key) =>
        System.Windows.Application.Current.TryFindResource(key) as string ?? key;
}
