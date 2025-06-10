using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using EdgeTTS;
using ImGuiNET;
using System.Numerics;

namespace IINACT.TextToSpeech;

public class EdgeTTSWindow : Window
{
    private readonly EdgeTTSManager _manager;
    private readonly object _lock = new();

    public EdgeTTSWindow(EdgeTTSManager manager) : base("EdgeTTS设置")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        _manager = manager;
    }

    public void Show()
    {
        IsOpen = true;
    }

    public override void Draw()
    {
        var config = _manager.GetConfig();
        var voices = _manager.GetAvailableVoices();
        var devices = _manager.GetAvailableDevices();

        // 语音选择
        var selectedVoiceIndex = Array.FindIndex(voices, v => v.Value == config.Voice);
        if (ImGui.BeginCombo("语音", selectedVoiceIndex >= 0 ? voices[selectedVoiceIndex].DisplayName : "未选择"))
        {
            for (var i = 0; i < voices.Length; i++)
            {
                if (ImGui.Selectable(voices[i].DisplayName, i == selectedVoiceIndex))
                {
                    _manager.UpdateConfig(c => c.Voice = voices[i].Value);
                }
            }
            ImGui.EndCombo();
        }

        // 语速调节
        var speed = config.Speed;
        if (ImGui.SliderInt("语速", ref speed, 0, 200))
        {
            _manager.UpdateConfig(c => c.Speed = speed);
        }

        // 音量调节
        var volume = config.Volume;
        if (ImGui.SliderInt("音量", ref volume, 0, 100))
        {
            _manager.UpdateConfig(c => c.Volume = volume);
        }

        // 语调调节
        var pitch = config.Pitch;
        if (ImGui.SliderInt("语调", ref pitch, 0, 200))
        {
            _manager.UpdateConfig(c => c.Pitch = pitch);
        }

        // 输出设备选择
        var selectedDeviceIndex = devices.FindIndex(d => d.Id == config.DeviceId);
        if (ImGui.BeginCombo("输出设备", selectedDeviceIndex >= 0 ? devices[selectedDeviceIndex].Name : "默认设备"))
        {
            if (ImGui.Selectable("默认设备", selectedDeviceIndex == -1))
            {
                _manager.UpdateConfig(c => c.DeviceId = 0);
            }
            for (var i = 0; i < devices.Count; i++)
            {
                if (ImGui.Selectable(devices[i].Name, i == selectedDeviceIndex))
                {
                    _manager.UpdateConfig(c => c.DeviceId = devices[i].Id);
                }
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 测试文本
        var testText = config.TestText;
        if (ImGui.InputTextMultiline("测试文本", ref testText, 1000, new Vector2(-1, 100)))
        {
            _manager.UpdateConfig(c => c.TestText = testText);
        }

        if (ImGui.Button("朗读测试"))
        {
            Task.Run(() => _manager.Speak(testText));
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 文本替换
        ImGui.Text("文本替换规则");
        var toRemove = -1;
        for (var i = 0; i < config.TextReplacements.Count; i++)
        {
            var replacement = config.TextReplacements.ElementAt(i);
            var from = replacement.Key;
            var to = replacement.Value;

            ImGui.PushID(i);
            if (ImGui.InputText("从", ref from, 100))
            {
                var newDict = new Dictionary<string, string>(config.TextReplacements);
                newDict.Remove(replacement.Key);
                newDict[from] = to;
                _manager.UpdateConfig(c => c.TextReplacements = newDict);
            }

            ImGui.SameLine();
            if (ImGui.InputText("到", ref to, 100))
            {
                var newDict = new Dictionary<string, string>(config.TextReplacements);
                newDict[replacement.Key] = to;
                _manager.UpdateConfig(c => c.TextReplacements = newDict);
            }

            ImGui.SameLine();
            if (ImGui.Button("删除"))
            {
                toRemove = i;
            }
            ImGui.PopID();
        }

        if (toRemove >= 0)
        {
            var newDict = new Dictionary<string, string>(config.TextReplacements);
            newDict.Remove(config.TextReplacements.ElementAt(toRemove).Key);
            _manager.UpdateConfig(c => c.TextReplacements = newDict);
        }

        if (ImGui.Button("添加替换规则"))
        {
            var newDict = new Dictionary<string, string>(config.TextReplacements);
            newDict[""] = "";
            _manager.UpdateConfig(c => c.TextReplacements = newDict);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("清理缓存"))
        {
            _manager.CleanupCache();
        }
    }
} 
