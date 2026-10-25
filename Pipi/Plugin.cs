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
    private readonly Features.BetterMarketBoard.Compat.BetterMarketBoardHost marketBoard;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        dockCollapser = new DockCollapser(config.DockCollapse, SaveConfig);
        marketBoard = new Features.BetterMarketBoard.Compat.BetterMarketBoardHost(windowSystem);
        configWindow = new ConfigWindow(config, dockCollapser, marketBoard, SaveConfig);
        windowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开设置。/pipi fold [on|off]：切换/折叠/展开所有浮动停靠组。" +
                          "/pipi market [物品名/ID]：开关市场布告板（或直接查看指定物品）。",
        });

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigWindow;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigWindow;
    }

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;

    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;

    [PluginService] internal static Dalamud.Plugin.Services.ICondition Condition { get; private set; } = null!;

    [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

    [PluginService] internal static IToastGui ToastGui { get; private set; } = null!;

    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;

    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    [PluginService] internal static Dalamud.Plugin.Services.ISeStringEvaluator SeStringEvaluator { get; private set; } = null!;

    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigWindow;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigWindow;
        CommandManager.RemoveHandler(CommandName);
        marketBoard.Dispose();
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

        if (Features.BetterMarketBoard.Compat.CommandManager.Instance().TryHandle(parts[0], args.Trim()[parts[0].Length..].Trim()))
            return;

        if (!parts[0].Equals("fold", StringComparison.OrdinalIgnoreCase))
        {
            ChatGui.PrintError($"未知参数：{args}。用法：{CommandName} fold [on|off] 或 {CommandName} market [物品名/ID]");
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
