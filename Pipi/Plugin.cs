using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Pipi.Features.DockCollapse;
using Pipi.Windows;

namespace Pipi;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/pipi";

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("Pipi");
    private readonly ConfigWindow configWindow;
    private readonly DockCollapser dockCollapser;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        dockCollapser = new DockCollapser(config.DockCollapse, SaveConfig);
        configWindow = new ConfigWindow(config, dockCollapser, SaveConfig);
        windowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开设置。/pipi fold [on|off]：切换/折叠/展开所有浮动停靠组。",
        });

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigWindow;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigWindow;
    }

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigWindow;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigWindow;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
    }

    private void OnDraw()
    {
        dockCollapser.Update();
        windowSystem.Draw();
    }

    private void OnCommand(string command, string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            ToggleConfigWindow();
            return;
        }

        if (!parts[0].Equals("fold", StringComparison.OrdinalIgnoreCase))
        {
            ChatGui.PrintError($"未知参数：{args}。用法：{CommandName} fold [on|off]");
            return;
        }

        switch (parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty)
        {
            case "":
                dockCollapser.RequestToggleAll();
                break;
            case "on":
                dockCollapser.RequestCollapseAll();
                break;
            case "off":
                dockCollapser.RequestExpandAll();
                break;
            default:
                ChatGui.PrintError($"未知参数：{parts[1]}。用法：{CommandName} fold [on|off]");
                break;
        }
    }

    private void ToggleConfigWindow() => configWindow.Toggle();

    private void SaveConfig() => PluginInterface.SavePluginConfig(config);
}
