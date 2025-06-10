using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using FFXIV_ACT_Plugin.Config;
using ImGuiNET;
using RainbowMage.OverlayPlugin;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace IINACT.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin { get; }

    private int selectedOverlayIndex;

    public MainWindow(Plugin plugin) : base($"IINACT v{plugin.Version}")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(307, 207),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public IPluginConfig? OverlayPluginConfig { get; set; }
    public IReadOnlyList<RainbowMage.OverlayPlugin.IOverlayTemplate>? OverlayPresets { get; set; }
    private string[]? OverlayNames => OverlayPresets?.Select(x => x.Name).ToArray();
    public RainbowMage.OverlayPlugin.WebSocket.ServerController? Server { get; set; }

    public void Dispose() { }

    public override void Draw()
    {
        using var bar = ImRaii.TabBar("settingsTabs");
        if (!bar) return;

        DrawMainWindow();
        DrawParseSettings();
        DrawWebSocketSettings();
        DrawTTSSettings();
    }

    private void DrawMainWindow()
    {
        using var tab = ImRaii.TabItem("运行状态");
        if (!tab) return;

        ImGui.Spacing();
        ImGui.TextColored(ImGuiColors.DalamudGrey, "OverlayPlugin 状态:");
        ImGuiHelpers.ScaledRelativeSameLine(155);
        ImGui.Text(Plugin.OverlayPluginStatus);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(ImGuiColors.DalamudGrey, "Overlay URI 生成器:");

        var comboWidth = ImGui.GetWindowWidth() * 0.8f;
        
        var selectedIndexOverlayName = OverlayNames?[selectedOverlayIndex] ?? "";
        var selectedOverlayName = Plugin.Configuration.SelectedOverlay ?? selectedIndexOverlayName;
        if (selectedOverlayName != selectedIndexOverlayName)
            for (var i = 0; i < OverlayNames?.Length; i++)
                if (OverlayNames?[i] == selectedOverlayName) 
                    selectedOverlayIndex = i;
        
        ImGui.SetNextItemWidth(comboWidth);
        if (ImGui.BeginCombo("悬浮窗", selectedOverlayName))
        {
            for (var i = 0; i < OverlayNames?.Length; i++)
            {
                var currentOverlayName = OverlayNames?[i] ?? "";
                if (ImGui.Selectable(currentOverlayName, currentOverlayName == selectedOverlayName))
                {
                    selectedOverlayIndex = i;
                    Plugin.Configuration.SelectedOverlay = currentOverlayName;
                    Plugin.Configuration.Save();
                }
            }

            ImGui.EndCombo();
        }

        var selectedOverlay = OverlayPresets?[selectedOverlayIndex];
        var overlayUri = selectedOverlay?.ToOverlayUri(new Uri($"ws://{Server?.Address}:{Server?.Port}/ws"));
        var overlayUriString = overlayUri?.ToString() ?? "<生成URI失败>";

        ImGui.SetNextItemWidth(comboWidth);
        ImGui.InputText("URI", ref overlayUriString, 1000, ImGuiInputTextFlags.ReadOnly);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        var serverStatus = Server is null ? "初始化中..." : "已停止";

        if (Server?.Running ?? false)
            serverStatus = $"监听 {Server?.Address}:{Server?.Port}";

        if (Server?.Failed ?? false)
        {
            serverStatus = Server.LastException?.Message ?? "失败";
            if (Server.LastException is SocketException { ErrorCode: 10048 })
                serverStatus = $"端口 {Server?.Port} 已被占用";
        }

        ImGui.TextColored(ImGuiColors.DalamudGrey, $"WebSocket 服务:");
        ImGuiHelpers.ScaledRelativeSameLine(155);
        ImGui.Text(serverStatus);
        ImGui.GetWindowDpiScale();

        if (Server?.Running ?? false)
        {
            if (ImGui.Button("停止"))
                Server.Stop();

            ImGui.SameLine();

            if (ImGui.Button("重启"))
                Server.Restart();
        }
        else if (Server is not null)
        {
            if (ImGui.Button("启动"))
                Server.Start();
        }
    }

     private void DrawParseSettings()
    {
        using var tab = ImRaii.TabItem("解析器");
        if (!tab) return;

        ImGui.Spacing();
        var elementWidth = ImGui.GetWindowWidth() - (150 * ImGuiHelpers.GlobalScale);
        var logFilePath = Plugin.Configuration.LogFilePath;
        ImGui.SetNextItemWidth(elementWidth);
        ImGui.InputText("日志文件路径", ref logFilePath, 200, ImGuiInputTextFlags.ReadOnly);
        ImGui.SameLine();
        if (ImGuiComponents.DisabledButton(FontAwesomeIcon.Folder))
        {
            Plugin.FileDialogManager.OpenFolderDialog("选择保存日志的文件夹", (success, path) =>
            {
                if (!success) return;
                Plugin.Configuration.LogFilePath = path;
                Plugin.Configuration.Save();
            }, Plugin.Configuration.LogFilePath);
        }
        ImGui.Spacing();
        ImGui.SetNextItemWidth(elementWidth);
        if (ImGui.BeginCombo("解析过滤器",
                             Enum.GetName(typeof(ParseFilterMode), Plugin.Configuration.ParseFilterMode)))
        {
            foreach (var filter in Enum.GetValues<ParseFilterMode>())
                if (ImGui.Selectable(Enum.GetName(typeof(ParseFilterMode), filter),
                                     (ParseFilterMode)Plugin.Configuration.ParseFilterMode == filter))
                {
                    Plugin.Configuration.ParseFilterMode = (int)filter;
                    Plugin.Configuration.Save();
                }

            ImGui.EndCombo();
        }

        ImGui.Spacing();
        
        var writeLogFile = Plugin.Configuration.WriteLogFile;
        if (ImGui.Checkbox("写入网络日志文件", ref writeLogFile))
        {
            Plugin.Configuration.WriteLogFile = writeLogFile;
            Plugin.Configuration.Save();
        }

        var disablePvp = Plugin.Configuration.DisablePvp;
        if (ImGui.Checkbox("在PvP中禁用写入网络日志文件", ref disablePvp))
        {
            if (Plugin.ClientState.IsPvP && disablePvp) Plugin.Configuration.DisableWritingPvpLogFile = true;

            Plugin.Configuration.DisablePvp = disablePvp;
            Plugin.Configuration.Save();
        }

        var disableDamageShield = Plugin.Configuration.DisableDamageShield;
        if (ImGui.Checkbox("禁用伤害盾估计", ref disableDamageShield))
        {
            Plugin.Configuration.DisableDamageShield = disableDamageShield;
            Plugin.Configuration.Save();
        }

        var disableCombinePets = Plugin.Configuration.DisableCombinePets;
        if (ImGui.Checkbox("禁用宠物合并", ref disableCombinePets))
        {
            Plugin.Configuration.DisableCombinePets = disableCombinePets;
            Plugin.Configuration.Save();
        }

        var showDebug = Plugin.Configuration.ShowDebug;
        if (ImGui.Checkbox("显示调试选项", ref showDebug))
        {
            Plugin.Configuration.ShowDebug = showDebug;
            Plugin.Configuration.Save();
        }

        if (!showDebug) return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var simulateIndividualDoTCrits = Plugin.Configuration.SimulateIndividualDoTCrits;
        if (ImGui.Checkbox("模拟单体 DoT 暴击", ref simulateIndividualDoTCrits))
        {
            Plugin.Configuration.SimulateIndividualDoTCrits = simulateIndividualDoTCrits;
            Plugin.Configuration.Save();
        }

        var showRealDoTTicks = Plugin.Configuration.ShowRealDoTTicks;
        if (ImGui.Checkbox("显示真实 DoT Ticks", ref showRealDoTTicks))
        {
            Plugin.Configuration.ShowRealDoTTicks = showRealDoTTicks;
            Plugin.Configuration.Save();
        }
    }

    private void DrawWebSocketSettings()
    {
        using var tab = ImRaii.TabItem("WebSocket 服务");
        if (!tab) return;
        
        ImGui.Spacing();
        var wsServerIp = OverlayPluginConfig?.WSServerIP ?? "";
        ImGui.InputText("IP地址", ref wsServerIp, 100, ImGuiInputTextFlags.None);

        if (IPAddress.TryParse(wsServerIp, out var address))
        {
            if (OverlayPluginConfig is not null)
                OverlayPluginConfig.WSServerIP = address.ToString();
        }
        else if (wsServerIp == "*")
        {
            if (OverlayPluginConfig is not null)
                OverlayPluginConfig.WSServerIP = "*";
        }

        var wsServerPort = OverlayPluginConfig?.WSServerPort.ToString() ?? "";
        ImGui.InputText("端口", ref wsServerPort, 100, ImGuiInputTextFlags.None);

        if (int.TryParse(wsServerPort, out var port))
        {
            if (OverlayPluginConfig is not null)
                OverlayPluginConfig.WSServerPort = port;
        }

        OverlayPluginConfig?.Save();
    }

    private void DrawTTSSettings()
    {
        using var tab = ImRaii.TabItem("文字转语音");
        if (!tab) return;

        ImGui.Spacing();
        var useEdgeTTS = Plugin.Configuration.UseEdgeTTS;
        if (ImGui.Checkbox("使用EdgeTTS（不勾选则使用本地TTS）", ref useEdgeTTS))
        {
            Plugin.Configuration.UseEdgeTTS = useEdgeTTS;
            Plugin.Configuration.Save();
            Plugin.TextToSpeechProvider.SetUseEdgeTTS(useEdgeTTS);
        }

        if (useEdgeTTS)
        {
            ImGui.SameLine();
            if (ImGui.Button("打开设置"))
            {
                Plugin.OpenEdgeTTSWindow();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

}
