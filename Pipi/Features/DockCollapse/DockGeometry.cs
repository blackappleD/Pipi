using System.Numerics;

namespace Pipi.Features.DockCollapse;

/// <summary>
/// Pure geometry helpers for dock collapsing. Kept free of ImGui types so they can be unit tested.
/// </summary>
internal static class DockGeometry
{
    /// <summary>Slack for float rounding and ImGui's own size clamping.</summary>
    public const float Tolerance = 2f;

    public static bool Contains(Vector2 min, Vector2 max, Vector2 point) =>
        point.X >= min.X && point.X < max.X && point.Y >= min.Y && point.Y < max.Y;

    /// <summary>
    /// Left edge of a tab, mirroring ImGui's TabItemEx layout: central tabs scroll with the bar,
    /// leading/trailing section tabs do not.
    /// </summary>
    public static float TabMinX(float barMinX, float tabOffset, float scrollingAnim, bool isCentralSection) =>
        isCentralSection
            ? barMinX + MathF.Truncate(tabOffset - scrollingAnim)
            : barMinX + tabOffset;

    public static bool IsWithinTab(float mouseX, float tabMinX, float tabWidth) =>
        mouseX >= tabMinX && mouseX < tabMinX + tabWidth;

    /// <summary>
    /// Only tab bars on the group's top edge may collapse it; shrinking the group from a lower
    /// split would squash the rows above instead of folding anything.
    /// </summary>
    public static bool IsOnTopEdge(float nodeTopY, float rootTopY) =>
        MathF.Abs(nodeTopY - rootTopY) <= Tolerance;

    /// <summary>
    /// A group counts as collapsed when it is no taller than the smallest height ImGui lets it reach,
    /// which may exceed the requested collapsed height because of the style's minimum window size.
    /// </summary>
    public static bool IsCollapsed(float currentHeight, float collapsedHeight, float minWindowHeight) =>
        currentHeight <= MathF.Max(collapsedHeight, minWindowHeight) + Tolerance;
}
