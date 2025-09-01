using System.IO;
using System.Text.Json;
using PixelAutomation.Core.Models;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public class ConfigurationManager
{
    private Configuration? _currentConfig;
    private List<string> _profileOrder = new();
    private int _currentProfileIndex = 0;

    public Configuration? LoadConfiguration(string path)
    {
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        _currentConfig = JsonSerializer.Deserialize<Configuration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (_currentConfig?.Profiles != null)
        {
            _profileOrder = _currentConfig.Profiles.Keys.ToList();
            _currentProfileIndex = 0;
        }

        return _currentConfig;
    }

    public void SaveConfiguration(string path, Configuration config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        File.WriteAllText(path, json);
        _currentConfig = config;
    }

    public void NextProfile()
    {
        if (_profileOrder.Count > 0)
        {
            _currentProfileIndex = (_currentProfileIndex + 1) % _profileOrder.Count;
        }
    }

    public void PreviousProfile()
    {
        if (_profileOrder.Count > 0)
        {
            _currentProfileIndex = (_currentProfileIndex - 1 + _profileOrder.Count) % _profileOrder.Count;
        }
    }

    public string? CurrentProfile => _profileOrder.Count > 0 ? _profileOrder[_currentProfileIndex] : null;
}