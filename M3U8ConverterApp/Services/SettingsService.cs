using System;
using System.IO;
using System.Text.Json;
using M3U8ConverterApp.Models;

namespace M3U8ConverterApp.Services;

internal interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

internal sealed class SettingsService : ISettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsPath;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "M3U8ConverterApp");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, SettingsFileName);
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignored intentionally. Failure to persist preferences should not crash the app.
        }
    }
}
