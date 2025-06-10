using System.Diagnostics;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using IINACT.TextToSpeech;

namespace IINACT;

internal class TextToSpeechProvider
{
    private string binary = "/usr/bin/say";
    private string args = "";
    private readonly object speechLock = new();
    private readonly SpeechSynthesizer? speechSynthesizer;
    private readonly EdgeTTSManager? edgeTTSManager;
    private bool useEdgeTTS = false;
    private readonly IPluginLog _log;
    
    public TextToSpeechProvider(IPluginLog log)
    {
        _log = log;
        try
        {
            speechSynthesizer = new SpeechSynthesizer();
            speechSynthesizer?.SetOutputToDefaultAudioDevice();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to initialize SAPI TTS engine");
        }

        try
        {
            edgeTTSManager = new EdgeTTSManager(log);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Failed to initialize EdgeTTS engine");
        }
        
        Advanced_Combat_Tracker.ActGlobals.oFormActMain.TextToSpeech += Speak;
    }

    public void SetUseEdgeTTS(bool useEdgeTTS)
    {
        this.useEdgeTTS = useEdgeTTS;
    }
    
    public void Speak(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (useEdgeTTS && edgeTTSManager != null)
        {
            try
            {
                Task.Run(() => edgeTTSManager.Speak(message));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"EdgeTTS failed to play back {message}");
            }
            return;
        }

        Task.Run(() =>
        {
            if (new FileInfo(binary).Exists)
            {
                try
                {
                    var ttsProcess = new Process
                    {
                        StartInfo =
                        {
                            FileName = "C:\\windows\\system32\\start.exe",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            Arguments = $"/unix {binary} {args} \"" +
                                        Regex.Replace(Regex.Replace(message, @"(\\*)" + "\"", @"$1$1\" + "\""),
                                                      @"(\\+)$", @"$1$1") + "\""
                        }
                    };
                    lock (speechLock)
                    {
                        ttsProcess.Start();
                        // heuristic pause
                        Thread.Sleep(500 * message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, $"Unix TTS failed to play back {message}");
                }
            }

            try
            {
                lock (speechLock)
                {
                    speechSynthesizer?.Speak(message);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"SAPI TTS failed to play back {message}");
            }
        });
    }

    public EdgeTTSManager? GetEdgeTTSManager() => edgeTTSManager;
}
