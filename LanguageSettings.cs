using System.Globalization;
using System.Text.Json;

namespace WindowSizerWinUI;

internal enum LanguageChoice
{
    SystemDefault,
    SimplifiedChinese,
    English
}

internal static class LanguageSettings
{
    private sealed class StoredSettings
    {
        public LanguageChoice Language { get; set; }
        public bool DarkMode { get; set; }
    }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetEnvironmentVariable("WINDOWSIZER_SETTINGS_ROOT") is string root &&
        !string.IsNullOrWhiteSpace(root)
            ? root
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowSizerWinUI"),
        "settings.json");

    public static LanguageChoice Language { get; private set; } = LanguageChoice.SystemDefault;
    public static bool DarkMode { get; private set; }

    public static bool IsEnglish => Language switch
    {
        LanguageChoice.English => true,
        LanguageChoice.SimplifiedChinese => false,
        _ => !IsSimplifiedChineseSystemLanguage()
    };

    static LanguageSettings()
    {
        try
        {
            StoredSettings? stored = JsonSerializer.Deserialize<StoredSettings>(
                File.ReadAllText(SettingsPath));
            if (stored is null) return;
            if (Enum.IsDefined(stored.Language)) Language = stored.Language;
            DarkMode = stored.DarkMode;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
    }

    public static string Text(string chinese, string english) => IsEnglish ? english : chinese;

    public static void SetLanguage(LanguageChoice language)
    {
        if (!Enum.IsDefined(language) || Language == language) return;
        Language = language;
        Save();
    }

    public static void SetDarkMode(bool enabled)
    {
        if (DarkMode == enabled) return;
        DarkMode = enabled;
        Save();
    }

    private static bool IsSimplifiedChineseSystemLanguage()
    {
        string name = CultureInfo.CurrentUICulture.Name;
        return name.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("zh-SG", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase);
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new StoredSettings
            {
                Language = Language,
                DarkMode = DarkMode
            }));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
