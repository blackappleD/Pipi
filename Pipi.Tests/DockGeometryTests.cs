using System.Numerics;
using Pipi.Features.DockCollapse;
using Xunit;

namespace Pipi.Tests;

public class DockGeometryTests
{
    [Theory]
    [InlineData(10, 10, true)]
    [InlineData(0, 0, true)]
    [InlineData(100, 20, false)] // max edge is exclusive, like ImRect::Contains
    [InlineData(-1, 10, false)]
    [InlineData(50, 25, false)]
    public void Contains_UsesHalfOpenRect(float x, float y, bool expected)
    {
        var result = DockGeometry.Contains(new Vector2(0, 0), new Vector2(100, 20), new Vector2(x, y));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TabMinX_CentralTabScrollsWithBar()
    {
        var minX = DockGeometry.TabMinX(barMinX: 100, tabOffset: 50, scrollingAnim: 20.7f, isCentralSection: true);

        Assert.Equal(129f, minX); // 100 + trunc(50 - 20.7)
    }

    [Fact]
    public void TabMinX_SectionTabIgnoresScrolling()
    {
        var minX = DockGeometry.TabMinX(barMinX: 100, tabOffset: 50, scrollingAnim: 20.7f, isCentralSection: false);

        Assert.Equal(150f, minX);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(179.9f, true)]
    [InlineData(180, false)]
    [InlineData(99.9f, false)]
    public void IsWithinTab_UsesTabWidth(float mouseX, bool expected)
    {
        Assert.Equal(expected, DockGeometry.IsWithinTab(mouseX, tabMinX: 100, tabWidth: 80));
    }

    [Theory]
    [InlineData(200, 200, true)]
    [InlineData(201.5f, 200, true)]
    [InlineData(230, 200, false)] // tab bar of a lower split
    public void IsOnTopEdge_OnlyAcceptsTopRow(float nodeTopY, float rootTopY, bool expected)
    {
        Assert.Equal(expected, DockGeometry.IsOnTopEdge(nodeTopY, rootTopY));
    }

    [Fact]
    public void IsCollapsed_TrueAtCollapsedHeight()
    {
        Assert.True(DockGeometry.IsCollapsed(currentHeight: 24, collapsedHeight: 24, minWindowHeight: 20));
    }

    [Fact]
    public void IsCollapsed_TrueWhenClampedUpToMinWindowHeight()
    {
        // ImGui refused to go below WindowMinSize.Y, so 32 is as collapsed as it gets.
        Assert.True(DockGeometry.IsCollapsed(currentHeight: 32, collapsedHeight: 24, minWindowHeight: 32));
    }

    [Fact]
    public void IsCollapsed_FalseForExpandedGroup()
    {
        Assert.False(DockGeometry.IsCollapsed(currentHeight: 300, collapsedHeight: 24, minWindowHeight: 32));
    }
}
