using System.Diagnostics;
using System.IO;
using System.Text.Json;
using ScreenSaver.Models;

namespace ScreenSaver.Core;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly string _configPath;

    public AppConfig Config { get; private set; }

    public ConfigService()
    {
        var exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
        _configPath = Path.Combine(exeDir, "config.json");
        Config = Load();
    }

    private AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        File.WriteAllText(_configPath, JsonSerializer.Serialize(config, _jsonOptions));
    }
}
