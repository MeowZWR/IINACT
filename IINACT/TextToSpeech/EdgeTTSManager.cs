using Dalamud.Plugin.Services;
using EdgeTTS;
using System.Diagnostics;

namespace IINACT.TextToSpeech;

public class EdgeTTSManager
{
    private readonly string _configPath;
    private string _cachePath = string.Empty;
    private readonly EdgeTTSConfig _config;
    private EdgeTTSEngine _engine;
    private readonly object _lock = new();
    private readonly IPluginLog _log;

    public string CurrentCachePath => _cachePath;

    public EdgeTTSManager(IPluginLog log, string configPath)
    {
        _log = log;
        _configPath = Path.Combine(Path.GetDirectoryName(configPath)!, "IINACT", "Notification", "TextToSpeech.json");
        _config = EdgeTTSConfig.Load(_configPath);
        
        UpdateCachePath(_config.CustomCachePath);
    }

    private void UpdateCachePath(string? customPath)
    {
        _cachePath = string.IsNullOrEmpty(customPath)
            ? Path.Combine(Path.GetDirectoryName(_configPath)!, "Cache")
            : customPath;

        if (!Directory.Exists(_cachePath))
            Directory.CreateDirectory(_cachePath);

        _engine = new EdgeTTSEngine(_cachePath, message => _log.Debug($"EdgeTTS: {message}"));
    }

    public void UpdateConfig(Action<EdgeTTSConfig> updateAction)
    {
        lock (_lock)
        {
            var oldCachePath = _config.CustomCachePath;
            updateAction(_config);
            _config.Save(_configPath);

            if (oldCachePath != _config.CustomCachePath)
            {
                UpdateCachePath(_config.CustomCachePath);
            }
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
            foreach (var file in Directory.GetFiles(_cachePath, "*.mp3"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // 忽略单个文件删除失败的情况
                }
            }
        }
        catch
        {
            // 忽略缓存清理失败的情况
        }
    }

    public void OpenCacheFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _cachePath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"无法打开缓存文件夹: {_cachePath}");
        }
    }

    public void MigrateCacheFiles(string newPath)
    {
        try
        {
            if (!Directory.Exists(newPath))
                Directory.CreateDirectory(newPath);

            foreach (var file in Directory.GetFiles(_cachePath, "*.mp3"))
            {
                try
                {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(newPath, fileName);
                    File.Move(file, destPath, true);
                }
                catch
                {
                    // 忽略单个文件迁移失败的情况
                }
            }
        }
        catch
        {
            // 忽略整体迁移失败的情况
        }
    }
} 
