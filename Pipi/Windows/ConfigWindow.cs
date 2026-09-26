using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Pipi.Features.DockCollapse;

namespace Pipi.Windows;

internal sealed class ConfigWindow : Window
{
    private readonly Configuration config;
    private readonly DockCollapser dockCollapser;
    private readonly Action saveConfig;

    public ConfigWindow(Configuration config, DockCollapser dockCollapser, Action saveConfig)
        : base("Pipi 设置###PipiConfig")
    {
        this.config = config;
        this.dockCollapser = dockCollapser;
        this.saveConfig = saveConfig;

        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(320, 160) };
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("停靠折叠");
        ImGui.Separator();

        var doubleClickEnabled = config.DockCollapse.DoubleClickEnabled;
        if (ImGui.Checkbox("双击标签栏空白处或右下角缩放手柄折叠/展开停靠组", ref doubleClickEnabled))
        {
            config.DockCollapse.DoubleClickEnabled = doubleClickEnabled;
            saveConfig();
        }

        if (ImGui.Button("全部折叠"))
            dockCollapser.RequestCollapseAll();
        ImGui.SameLine();
        if (ImGui.Button("全部展开"))
            dockCollapser.RequestExpandAll();

        ImGui.Spacing();
        ImGui.TextDisabled("指令：/pipi fold 切换，/pipi fold on 全部折叠，/pipi fold off 全部展开");
        ImGui.TextDisabled("可以把指令写进用户宏，放到热键栏上当快捷键用。");
    }
}
