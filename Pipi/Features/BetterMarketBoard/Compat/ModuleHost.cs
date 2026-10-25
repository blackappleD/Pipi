// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi; provides the small subset of the Daily Routines module host the port relies on.
// SPDX-License-Identifier: AGPL-3.0-only

using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Newtonsoft.Json;

namespace Pipi.Features.BetterMarketBoard.Compat;

public enum ModuleCategory
{
    General,
    Interface,
}

/// <summary>Display metadata. Kept so the ported module compiles unchanged; Pipi does not show it.</summary>
public sealed class ModuleInfo
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public ModuleCategory Category { get; init; }

    public string[] Author { get; init; } = [];

    public string[] ModulesPrerequisite { get; init; } = [];

    public string[] ModulesPair { get; init; } = [];

    public string[] PreviewImageURL { get; init; } = [];
}

public sealed class ModulePermission
{
    public bool NeedAuth { get; init; }

    public bool AllDefaultEnabled { get; init; }
}

/// <summary>Base for module configs; persisted as JSON in Pipi's config directory.</summary>
public abstract class ModuleConfig
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Formatting = Formatting.Indented,
    };

    public void Save(ModuleBase module)
    {
        try
        {
            File.WriteAllText(module.ConfigFilePath, JsonConvert.SerializeObject(this, GetType(), Settings));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to save config of {module.ModuleName}");
        }
    }

    internal static T? LoadFrom<T>(ModuleBase module) where T : ModuleConfig
    {
        try
        {
            return File.Exists(module.ConfigFilePath)
                ? JsonConvert.DeserializeObject<T>(File.ReadAllText(module.ConfigFilePath), Settings)
                : null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to load config of {module.ModuleName}");
            return null;
        }
    }
}

public static class ModuleConfigExtensions
{
    extension<T>(T) where T : ModuleConfig
    {
        /// <summary>Lets the module write <c>Config.Load(this)</c>.</summary>
        public static T? Load(ModuleBase module) => ModuleConfig.LoadFrom<T>(module);
    }
}

/// <summary>Floating window owned by a module; draws <see cref="ModuleBase.OverlayUI"/>.</summary>
public sealed class ModuleOverlay : Window
{
    private readonly ModuleBase module;

    public ModuleOverlay(ModuleBase module)
        : base($"{module.ModuleName}###Pipi-{module.ModuleName}-Overlay")
    {
        this.module = module;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(300, 200) };
    }

    public override void Draw()
    {
        using (FontManager.Instance().UIFont.Push())
            module.DrawOverlay();
    }
}

/// <summary>
/// Minimal stand-in for the Daily Routines module base: lifecycle hooks, an overlay window, a task queue,
/// and a per-module config file. Hosted by <see cref="BetterMarketBoardHost"/>.
/// </summary>
public abstract class ModuleBase
{
    public virtual ModuleInfo Info { get; } = new();

    public virtual ModulePermission Permission { get; } = new();

    public string ModuleName => GetType().Name;

    public string ConfigFilePath => Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), $"{ModuleName}.json");

    public ModuleOverlay? Overlay { get; protected set; }

    public TaskHelper? TaskHelper { get; protected set; }

    internal bool HasConfigUI => GetType().GetMethod(
        nameof(ConfigUI),
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly) != null;

    internal void Load() => Init();

    internal void Unload()
    {
        Uninit();
        TaskHelper?.Dispose();
        TaskHelper = null;
    }

    internal void DrawConfig() => ConfigUI();

    internal void DrawOverlay() => OverlayUI();

    protected virtual void Init()
    {
    }

    protected virtual void Uninit()
    {
    }

    protected virtual void ConfigUI()
    {
    }

    protected virtual void OverlayUI()
    {
    }
}

/// <summary>Creates the module, registers its overlay with Pipi's window system and tears it down again.</summary>
internal sealed class BetterMarketBoardHost : IDisposable
{
    private readonly WindowSystem windowSystem;
    private readonly ModuleBase module;

    public BetterMarketBoardHost(WindowSystem windowSystem)
    {
        this.windowSystem = windowSystem;
        FontManager.Instance().Initialize();
        TooltipManager.Instance().Initialize();
        GameState.Instance().Initialize();
        ContextMenuManager.Instance().Initialize();

        module = new BetterMarketBoard();
        module.Load();
        if (module.Overlay is { } overlay)
            windowSystem.AddWindow(overlay);
    }

    public ModuleOverlay? Overlay => module.Overlay;

    public void DrawConfig()
    {
        using (FontManager.Instance().UIFont.Push())
            module.DrawConfig();
    }

    public void Dispose()
    {
        if (module.Overlay is { } overlay)
            windowSystem.RemoveWindow(overlay);
        module.Unload();

        ContextMenuManager.Instance().Dispose();
        GameState.Instance().Dispose();
        TooltipManager.Instance().Dispose();
        FrameworkManager.Instance().Dispose();
        LinkPayloadManager.Instance().Dispose();
        FontManager.Instance().Dispose();
        Universalis.Shutdown();
    }
}
