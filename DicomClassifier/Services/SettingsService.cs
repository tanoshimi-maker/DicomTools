using System.Text.Json;
using DicomClassifier.Models;

namespace DicomClassifier.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DicomToolkits",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Ignore corrupt settings
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silently fail on save
        }
    }
}
