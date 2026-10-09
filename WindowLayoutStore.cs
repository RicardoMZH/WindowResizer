using System.Text.Json;
using Windows.Graphics;

namespace WindowSizerWinUI;

internal static class WindowLayoutStore
{
    private readonly record struct SavedSize(int Width, int Height);
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowSizerWinUI", "window.json");

    public static SizeInt32 Load()
    {
        try
        {
            SavedSize size = JsonSerializer.Deserialize<SavedSize>(File.ReadAllText(SettingsFile));
            if (size.Width is >= 200 and <= 20000 && size.Height is >= 150 and <= 20000)
                return new SizeInt32(size.Width, size.Height);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        return new SizeInt32(1600, 900);
    }

    public static bool Save(SizeInt32 size)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new SavedSize(size.Width, size.Height)));
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
