using Dalamud.Configuration;

namespace Pipi;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public DockCollapseConfig DockCollapse { get; set; } = new();
}

[Serializable]
public sealed class DockCollapseConfig
{
    public bool DoubleClickEnabled { get; set; } = true;

    /// <summary>Height to restore on expand, keyed by root dock node ID.</summary>
    public Dictionary<uint, float> ExpandedHeights { get; set; } = new();
}
