using Dalamud.Interface.Windowing;
using ImGuiNET;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;

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

        // 两列布局
        ImGui.Columns(2, "EdgeTTSSettingsColumns", true);

        // --- 左列：语音选择 ---
        ImGui.Separator();

        using (var child = ImRaii.Child("VoiceListChild", new Vector2(ImGui.GetContentRegionAvail().X, -1), true))
        {
            if (child)
            {
                for (var i = 0; i < voices.Length; i++)
                {
                    var isSelected = voices[i].Value == config.Voice;
                    if (ImGui.Selectable(voices[i].DisplayName, isSelected))
                    {
                        _manager.UpdateConfig(c => c.Voice = voices[i].Value);
                    }
                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
            }
        }

        ImGui.NextColumn();

        // --- 右列：其他设置 ---
        ImGui.AlignTextToFramePadding();
        ImGui.Text("测试文本");
        ImGui.SameLine();
        if (ImGui.Button("朗读"))
        {
            Task.Run(() => _manager.Speak(config.TestText));
        }
        
        var testText = config.TestText;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##TestTextInput", ref testText, 1000)) 
        {
            _manager.UpdateConfig(c => c.TestText = testText);
        }
        ImGui.Spacing();
        
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X);
        ImGui.TextWrapped(testText);
        ImGui.PopTextWrapPos();
        ImGui.Spacing();

        ImGui.AlignTextToFramePadding();
        ImGui.Text("语速:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.7f);
        var speed = config.Speed;
        if (ImGui.SliderInt("##SpeedSlider", ref speed, 1, 200))
        {
            _manager.UpdateConfig(c => c.Speed = speed);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text("语调:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.7f);
        var pitch = config.Pitch;
        if (ImGui.SliderInt("##PitchSlider", ref pitch, 1, 200))
        {
            _manager.UpdateConfig(c => c.Pitch = pitch);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text("音量:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.7f);
        var volume = config.Volume;
        if (ImGui.SliderInt("##VolumeSlider", ref volume, 1, 100))
        {
            _manager.UpdateConfig(c => c.Volume = volume);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text("输出设备:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var selectedDeviceIndex = devices.FindIndex(d => d.Id == config.DeviceId);
        if (ImGui.BeginCombo("##OutputDeviceCombo", selectedDeviceIndex >= 0 ? devices[selectedDeviceIndex].Name : "默认设备"))
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

        ImGui.Text("文本替换规则");
        if (ImGui.BeginTable("TextReplacementTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn(" 写作");
            ImGui.TableSetupColumn(" 读作");
            ImGui.TableSetupColumn(" 操作");
            ImGui.TableHeadersRow();

            var toRemove = -1;
            for (var i = 0; i < config.TextReplacements.Count; i++)
            {
                var replacement = config.TextReplacements.ElementAt(i);
                var from = replacement.Key;
                var to = replacement.Value;

                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##From" + i, ref from, 100))
                {
                    var newDict = new Dictionary<string, string>(config.TextReplacements);
                    newDict.Remove(replacement.Key);
                    newDict[from] = to;
                    _manager.UpdateConfig(c => c.TextReplacements = newDict);
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##To" + i, ref to, 100))
                {
                    var newDict = new Dictionary<string, string>(config.TextReplacements);
                    newDict[replacement.Key] = to;
                    _manager.UpdateConfig(c => c.TextReplacements = newDict);
                }

                ImGui.TableNextColumn();
                if (ImGui.Button("删除##" + i))
                {
                    toRemove = i;
                }
                ImGui.PopID();
            }

            ImGui.EndTable();

            if (toRemove >= 0)
            {
                var newDict = new Dictionary<string, string>(config.TextReplacements);
                newDict.Remove(config.TextReplacements.ElementAt(toRemove).Key);
                _manager.UpdateConfig(c => c.TextReplacements = newDict);
            }
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

        if (ImGui.Button("打开缓存文件夹"))
        {
            _manager.OpenCacheFolder();
        }
        ImGui.SameLine();
        if (ImGui.Button("清理缓存"))
        {
            _manager.CleanupCache();
        }

        ImGui.Columns(1);
    }
} 
