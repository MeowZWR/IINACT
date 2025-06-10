using Dalamud.Plugin.Services;
using EdgeTTS;

namespace IINACT.TextToSpeech;

public class EdgeTTSManager
{
    private readonly string _configPath;
    private readonly string _cachePath;
    private readonly EdgeTTSConfig _config;
    private readonly EdgeTTSEngine _engine;
    private readonly object _lock = new();
    private readonly IPluginLog _log;

    public EdgeTTSManager(IPluginLog log)
    {
        _log = log;
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncherCN", "pluginConfigs", "IINACT");
        _configPath = Path.Combine(configDir, "Notification", "TextToSpeech.json");
        _cachePath = Path.Combine(configDir, "Notification", "Cache");
        
        if (!Directory.Exists(_cachePath))
            Directory.CreateDirectory(_cachePath);

        _config = EdgeTTSConfig.Load(_configPath);
        _engine = new EdgeTTSEngine(_cachePath, message => _log.Debug($"EdgeTTS: {message}"));
    }

    public void UpdateConfig(Action<EdgeTTSConfig> updateAction)
    {
        lock (_lock)
        {
            updateAction(_config);
            _config.Save(_configPath);
        }
    }

    public EdgeTTSConfig GetConfig() => _config;

    public Voice[] GetAvailableVoices() => EdgeTTSEngine.Voices;

    public List<AudioDevice> GetAvailableDevices() => EdgeTTSEngine.GetAudioDevices();

    public async Task Speak(string text)
    {
        var settings = _config.ToEdgeTTSSettings();
        await _engine.SpeakAsync(text, settings);
    }

    public void CleanupCache()
    {
        try
        {
            var files = Directory.GetFiles(_cachePath, "*.mp3");
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore errors when deleting individual files
                }
            }
        }
        catch
        {
            // Ignore errors when cleaning up cache
        }
    }
} 
