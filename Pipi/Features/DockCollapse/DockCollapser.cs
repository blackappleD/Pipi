using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Pipi.Features.DockCollapse;

/// <summary>
/// ImGui cannot collapse docked windows, so this shrinks a floating dock group down to its tab bar
/// and restores it later. All ImGui access happens inside <see cref="Update"/>, i.e. during the frame.
/// </summary>
internal sealed class DockCollapser
{
    private const float FallbackExpandedHeight = 300f;

    private readonly DockCollapseConfig config;
    private readonly Action saveConfig;
    // Written by chat commands, consumed during the ImGui frame.
    private int pendingBulkAction = (int)BulkAction.None;

    public DockCollapser(DockCollapseConfig config, Action saveConfig)
    {
        this.config = config;
        this.saveConfig = saveConfig;
    }

    private enum BulkAction
    {
        None,
        Toggle,
        CollapseAll,
        ExpandAll,
    }

    public void RequestToggleAll() => Interlocked.Exchange(ref pendingBulkAction, (int)BulkAction.Toggle);

    public void RequestCollapseAll() => Interlocked.Exchange(ref pendingBulkAction, (int)BulkAction.CollapseAll);

    public void RequestExpandAll() => Interlocked.Exchange(ref pendingBulkAction, (int)BulkAction.ExpandAll);

    /// <summary>Called once per frame from UiBuilder.Draw.</summary>
    public void Update()
    {
        var bulkAction = (BulkAction)Interlocked.Exchange(ref pendingBulkAction, (int)BulkAction.None);
        if (bulkAction != BulkAction.None)
        {
            // Skip double-click handling this frame so one group is never toggled twice.
            RunBulkAction(bulkAction);
            return;
        }

        // The only per-frame cost: a flag read. Everything below runs on the double-click frame only.
        if (!config.DoubleClickEnabled || !ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            return;

        var root = FindDoubleClickedRoot();
        if (root.IsNull)
            return;

        if (IsCollapsed(root))
            Expand(root);
        else
            Collapse(root);
        PruneAndSave();
    }

    private void RunBulkAction(BulkAction action)
    {
        var roots = FindVisibleFloatingRoots();
        if (roots.Count == 0)
            return;

        var collapse = action switch
        {
            BulkAction.CollapseAll => true,
            BulkAction.ExpandAll => false,
            _ => roots.Exists(root => !IsCollapsed(root)),
        };

        foreach (var root in roots)
        {
            if (collapse && !IsCollapsed(root))
                Collapse(root);
            else if (!collapse && IsCollapsed(root))
                Expand(root);
        }
        PruneAndSave();
    }

    /// <summary>
    /// ImGui recycles dock node IDs, so heights saved for nodes that no longer exist are dropped
    /// before an unrelated group can inherit them.
    /// </summary>
    private void PruneAndSave()
    {
        var staleIds = config.ExpandedHeights.Keys.Where(id => ImGuiP.DockBuilderGetNode(id).IsNull).ToList();
        foreach (var id in staleIds)
            config.ExpandedHeights.Remove(id);
        saveConfig();
    }

    private static ImGuiDockNodePtr FindDoubleClickedRoot()
    {
        var ctx = ImGui.GetCurrentContext();
        var node = ctx.HoveredDockNode;
        if (node.IsNull || node.TabBar.IsNull || ImGuiP.IsNoTabBar(node) || ImGuiP.IsHiddenTabBar(node))
            return ImGuiDockNodePtr.Null;

        var mouse = ImGui.GetMousePos();
        var tabBar = node.TabBar;
        if (!DockGeometry.Contains(tabBar.BarRect.Min, tabBar.BarRect.Max, mouse) || IsOverTab(tabBar, mouse.X, ctx.FrameCount))
            return ImGuiDockNodePtr.Null;

        var root = ImGuiP.DockNodeGetRootNode(node);
        if (!IsCollapsible(root, ctx.FrameCount) || !DockGeometry.IsOnTopEdge(node.Pos.Y, root.Pos.Y))
            return ImGuiDockNodePtr.Null;

        return root;
    }

    private static bool IsOverTab(ImGuiTabBarPtr tabBar, float mouseX, int frameCount)
    {
        const ImGuiTabItemFlags sectionMask = ImGuiTabItemFlags.Leading | ImGuiTabItemFlags.Trailing;

        foreach (var tab in tabBar.Tabs.AsSpan())
        {
            // Tabs not laid out last frame have stale offsets.
            if (tab.LastFrameVisible < frameCount - 1)
                continue;

            var isCentral = (tab.Flags & sectionMask) == 0;
            var minX = DockGeometry.TabMinX(tabBar.BarRect.Min.X, tab.Offset, tabBar.ScrollingAnim, isCentral);
            if (DockGeometry.IsWithinTab(mouseX, minX, tab.Width))
                return true;
        }
        return false;
    }

    private static List<ImGuiDockNodePtr> FindVisibleFloatingRoots()
    {
        var ctx = ImGui.GetCurrentContext();
        var roots = new List<ImGuiDockNodePtr>();
        foreach (var window in ctx.Windows.AsSpan())
        {
            var node = window.DockNodeAsHost;
            if (IsCollapsible(node, ctx.FrameCount))
                roots.Add(node);
        }
        return roots;
    }

    /// <summary>
    /// A floating group whose host window was drawn recently. A floating node holding a single window
    /// hides its host and shows the window with a normal title bar, which ImGui can already collapse.
    /// </summary>
    private static bool IsCollapsible(ImGuiDockNodePtr root, int frameCount)
    {
        if (root.IsNull || !ImGuiP.IsFloatingNode(root))
            return false;

        var host = root.HostWindow;
        if (host.IsNull || host.LastFrameActive < frameCount - 1)
            return false;

        return !ImGuiP.IsLeafNode(root)
            || (!root.TabBar.IsNull && !ImGuiP.IsNoTabBar(root) && !ImGuiP.IsHiddenTabBar(root));
    }

    /// <summary>Height of the dock tab bar, as laid out by ImGui's DockNodeCalcTabBarLayout.</summary>
    private static float CollapsedHeight(ImGuiDockNodePtr root)
    {
        var borderSize = root.HostWindow.IsNull ? 0f : root.HostWindow.WindowBorderSize;
        return ImGui.GetFrameHeight() + borderSize;
    }

    private static bool IsCollapsed(ImGuiDockNodePtr root)
    {
        var minWindowHeight = ImGui.GetCurrentContext().Style.WindowMinSize.Y;
        return DockGeometry.IsCollapsed(root.Size.Y, CollapsedHeight(root), minWindowHeight);
    }

    private void Collapse(ImGuiDockNodePtr root)
    {
        config.ExpandedHeights[root.ID] = root.Size.Y;
        Resize(root, root.Size with { Y = CollapsedHeight(root) });
    }

    private void Expand(ImGuiDockNodePtr root)
    {
        // Saved heights survive restarts because ImGui persists dock node IDs in its ini file;
        // the fallback covers groups collapsed by hand or before this plugin was installed.
        var height = config.ExpandedHeights.Remove(root.ID, out var saved)
            ? saved
            : FallbackExpandedHeight * ImGuiHelpers.GlobalScale;
        Resize(root, root.Size with { Y = height });
    }

    /// <summary>
    /// A floating host window is re-sized from the node every frame, so both must be updated
    /// or the change is reverted on the next frame.
    /// </summary>
    private static void Resize(ImGuiDockNodePtr root, Vector2 size)
    {
        root.Size = size;
        root.SizeRef = size;
        if (!root.HostWindow.IsNull)
            ImGuiP.SetWindowSize(root.HostWindow, size, ImGuiCond.Always);
    }
}
