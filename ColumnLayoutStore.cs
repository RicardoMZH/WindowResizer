using System.Text.Json;

namespace WindowSizerWinUI;

internal static class ColumnLayoutStore
{
    private static readonly double[] Defaults = [300, 130, 105];
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowSizerWinUI", "columns.json");

    public static double[] Load()
    {
        try
        {
            double[]? widths = JsonSerializer.Deserialize<double[]>(File.ReadAllText(SettingsFile));
            if (widths is { Length: 3 } && widths.All(x => double.IsFinite(x) && x >= 100 && x <= 10000))
                return widths;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        return (double[])Defaults.Clone();
    }

    public static bool Save(double[] widths)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(widths));
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
