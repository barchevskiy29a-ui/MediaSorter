using System.IO;
using Microsoft.Extensions.Logging;
using MediaSorter.Models;
using System.Text.Json;

namespace MediaSorter.Services.Settings;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    event Action<AppSettings>? SettingsChanged;
    AppSettings Current { get; }
    void Update(Action<AppSettings> updateAction);
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly ILogger<SettingsService> _logger;
    private AppSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public event Action<AppSettings>? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appData, "MediaSorter");
        Directory.CreateDirectory(settingsDir);
        _settingsPath = Path.Combine(settingsDir, "settings.json");
        _settings = Load();
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                _logger.LogInformation("Настройки загружены из {Path}", _settingsPath);
                return settings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось загрузить настройки, используются значения по умолчанию");
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            _settings = settings;
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
            _logger.LogInformation("Настройки сохранены в {Path}", _settingsPath);
            SettingsChanged?.Invoke(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось сохранить настройки");
        }
    }

    public AppSettings Current => _settings;
    
    public void Update(Action<AppSettings> updateAction)
    {
        var newSettings = _settings with { };
        updateAction(newSettings);
        Save(newSettings);
    }
}