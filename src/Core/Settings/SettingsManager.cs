using System.Text.Json;

namespace StepsRecorder.Core.Settings;

public static class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string DefaultPath =>
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static string TemplatePath =>
        Path.Combine(AppContext.BaseDirectory, "settings.template.json");

    public static AppSettings Load(string? path = null)
    {
        var target = path ?? DefaultPath;

        if (!File.Exists(target))
        {
            if (File.Exists(TemplatePath))
                File.Copy(TemplatePath, target);
            else
                return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(target);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        var target = path ?? DefaultPath;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(target, json);
    }
}
