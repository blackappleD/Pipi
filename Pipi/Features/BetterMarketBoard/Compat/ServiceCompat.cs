// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi: framework ticks, commands, context menu, chat links, notifications, fonts and the task queue.
// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Text.ReadOnly;

namespace Pipi.Features.BetterMarketBoard.Compat;

/// <summary>Runs callbacks on framework ticks, optionally throttled to an interval.</summary>
public sealed class FrameworkManager : IDisposable
{
    private static readonly FrameworkManager Shared = new();

    private readonly List<(Action<IFramework> Action, long IntervalMs, long LastRun)> entries = [];
    private bool subscribed;

    public static FrameworkManager Instance() => Shared;

    public void Reg(Action<IFramework> action, int intervalMs = 0)
    {
        entries.Add((action, intervalMs, 0));
        if (subscribed)
            return;
        Plugin.Framework.Update += OnUpdate;
        subscribed = true;
    }

    public void Unreg(params Action<IFramework>[] actions)
    {
        foreach (var action in actions)
            entries.RemoveAll(e => e.Action == action);
    }

    public void Dispose()
    {
        entries.Clear();
        if (subscribed)
            Plugin.Framework.Update -= OnUpdate;
        subscribed = false;
    }

    private void OnUpdate(IFramework framework)
    {
        var now = Environment.TickCount64;
        for (var i = 0; i < entries.Count; i++)
        {
            var (action, interval, lastRun) = entries[i];
            if (interval > 0 && now - lastRun < interval)
                continue;

            entries[i] = (action, interval, now);
            try
            {
                action(framework);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "BetterMarketBoard framework callback failed");
            }
        }
    }
}

/// <summary>Sub-commands of <c>/pipi</c> registered by the module (e.g. <c>/pipi market</c>).</summary>
public sealed class CommandManager
{
    private static readonly CommandManager Shared = new();

    private readonly Dictionary<string, CommandInfo> subCommands = new(StringComparer.OrdinalIgnoreCase);

    public static CommandManager Instance() => Shared;

    public IReadOnlyDictionary<string, CommandInfo> SubCommands => subCommands;

    public void AddSubCommand(string name, CommandInfo info) => subCommands[name] = info;

    public void RemoveSubCommand(string name) => subCommands.Remove(name);

    public bool TryHandle(string name, string args)
    {
        if (!subCommands.TryGetValue(name, out var info))
            return false;
        info.Handler($"/pipi {name}", args);
        return true;
    }
}

public sealed class ContextMenuOpenedArgs
{
    public required IMenuOpenedArgs Raw { get; init; }

    public uint TargetItemID { get; init; }

    public RowRef<Lumina.Excel.Sheets.Item> TargetItemRow => LuminaCreateRef(TargetItemID);

    private static RowRef<Lumina.Excel.Sheets.Item> LuminaCreateRef(uint itemId) => new(Plugin.DataManager.Excel, itemId);
}

public sealed class ContextMenuItem
{
    public string Name { get; init; } = string.Empty;

    public Action<IMenuItemClickedArgs>? OnClicked { get; init; }
}

public abstract class ContextMenuEntry
{
    public abstract string Identifier { get; }

    public abstract ContextMenuItem? Create(ContextMenuOpenedArgs args);
}

/// <summary>Adds module entries to item context menus (inventory, chat item links, crafting log).</summary>
public sealed unsafe class ContextMenuManager : IDisposable
{
    private static readonly ContextMenuManager Shared = new();

    private readonly List<ContextMenuEntry> entries = [];
    private bool subscribed;

    public static ContextMenuManager Instance() => Shared;

    public void Reg(ContextMenuEntry entry) => entries.Add(entry);

    public void Unreg(ContextMenuEntry entry) => entries.Remove(entry);

    internal void Initialize()
    {
        if (subscribed)
            return;
        Plugin.ContextMenu.OnMenuOpened += OnMenuOpened;
        subscribed = true;
    }

    public void Dispose()
    {
        if (subscribed)
            Plugin.ContextMenu.OnMenuOpened -= OnMenuOpened;
        subscribed = false;
        entries.Clear();
    }

    private void OnMenuOpened(IMenuOpenedArgs raw)
    {
        var args = new ContextMenuOpenedArgs { Raw = raw, TargetItemID = ResolveItemId(raw) };
        foreach (var entry in entries)
        {
            var item = entry.Create(args);
            if (item is null)
                continue;

            raw.AddMenuItem(new MenuItem
            {
                Name = item.Name,
                PrefixChar = 'P',
                OnClicked = clicked => item.OnClicked?.Invoke(clicked),
            });
        }
    }

    private static uint ResolveItemId(IMenuOpenedArgs args)
    {
        if (args.MenuType == ContextMenuType.Inventory)
            return args.Target is MenuTargetInventory { TargetItem: { } item } ? item.BaseItemId : 0;

        uint raw;
        switch (args.AddonName)
        {
            case "ChatLog":
            {
                var agent = AgentChatLog.Instance();
                raw = agent == null ? 0 : agent->ContextItemId;
                break;
            }
            case "RecipeNote":
            case "RecipeTree":
            case "RecipeMaterialList":
            {
                var agent = AgentRecipeNote.Instance();
                raw = agent == null ? 0 : agent->ContextMenuResultItemId;
                break;
            }
            default:
                return 0;
        }

        return ItemIds.Normalize(raw, out _);
    }
}

/// <summary>Raw item IDs carry quality/kind offsets; this strips them.</summary>
internal static class ItemIds
{
    private const uint HqOffset = 1_000_000;
    private const uint CollectibleOffset = 500_000;
    private const uint EventItemOffset = 2_000_000;

    public static uint Normalize(uint raw, out ItemKind kind)
    {
        switch (raw)
        {
            case >= EventItemOffset:
                kind = ItemKind.EventItem;
                return raw;
            case >= HqOffset:
                kind = ItemKind.Hq;
                return raw - HqOffset;
            case >= CollectibleOffset:
                kind = ItemKind.Collectible;
                return raw - CollectibleOffset;
            default:
                kind = ItemKind.Normal;
                return raw;
        }
    }
}

/// <summary>Clickable chat links backed by Dalamud link handlers.</summary>
public sealed class LinkPayloadManager : IDisposable
{
    private static readonly LinkPayloadManager Shared = new();

    private readonly Dictionary<uint, DalamudLinkPayload> payloads = [];
    private uint nextId = 1;

    public static LinkPayloadManager Instance() => Shared;

    public DalamudLinkPayload Reg(Action<uint, SeString> handler, out uint id)
    {
        id = nextId++;
        var payload = Plugin.ChatGui.AddChatLinkHandler(id, handler);
        payloads[id] = payload;
        return payload;
    }

    public bool TryGetPayload(uint id, out DalamudLinkPayload payload) => payloads.TryGetValue(id, out payload!);

    public void Unreg(uint id)
    {
        if (payloads.Remove(id))
            Plugin.ChatGui.RemoveChatLinkHandler(id);
    }

    public void Dispose()
    {
        foreach (var id in payloads.Keys.ToList())
            Unreg(id);
    }
}

public sealed class NotifyHelper
{
    private static readonly NotifyHelper Shared = new();

    public static NotifyHelper Instance() => Shared;

    public static void Toast(ReadOnlySeString message) => Plugin.ToastGui.ShowNormal(message.ToDalamud());

    public void Chat(ReadOnlySeString message) => Chat(message.ToDalamud());

    public void Chat(SeString message) => Plugin.ChatGui.Print(new XivChatEntry { Message = message, Type = XivChatType.Echo });

    public void Chat(string message) => Plugin.ChatGui.Print(message);

    public void NotificationSuccess(string message) =>
        Plugin.NotificationManager.AddNotification(new Notification { Content = message, Type = NotificationType.Success });
}

public sealed unsafe class ChatManager
{
    private static readonly ChatManager Shared = new();

    public static ChatManager Instance() => Shared;

    /// <summary>Sends a line as if typed into the chat box (used for <c>/pdr worldtravel</c> when Daily Routines is installed).</summary>
    public void SendMessage(string message)
    {
        var uiModule = UIModule.Instance();
        if (uiModule == null || string.IsNullOrWhiteSpace(message))
            return;

        var text = Utf8String.FromString(message);
        try
        {
            uiModule->ProcessChatBoxEntry(text);
        }
        finally
        {
            text->Dtor(true);
        }
    }
}

public sealed class Throttler
{
    public static readonly Throttler Shared = new();

    private readonly Dictionary<string, long> nextAllowed = [];

    public bool Throttle(string key, int intervalMs = 500)
    {
        var now = Environment.TickCount64;
        if (nextAllowed.TryGetValue(key, out var allowedAt) && now < allowedAt)
            return false;
        nextAllowed[key] = now + intervalMs;
        return true;
    }
}

/// <summary>Scaled copies of Dalamud's default font, named after their size relative to it.</summary>
public sealed class FontManager : IDisposable
{
    private static readonly FontManager Shared = new();

    public static FontManager Instance() => Shared;

    public IFontHandle UIFont { get; private set; } = null!;

    public IFontHandle UIFont60 { get; private set; } = null!;

    public IFontHandle UIFont80 { get; private set; } = null!;

    public IFontHandle UIFont160 { get; private set; } = null!;

    internal void Initialize()
    {
        UIFont = Create(1f);
        UIFont60 = Create(0.6f);
        UIFont80 = Create(0.8f);
        UIFont160 = Create(1.6f);
    }

    public void Dispose()
    {
        UIFont?.Dispose();
        UIFont60?.Dispose();
        UIFont80?.Dispose();
        UIFont160?.Dispose();
    }

    // The module writes FontAwesome glyphs inline with text (e.g. a bell icon followed by a label) without
    // switching fonts, so the icon glyphs are merged into every text font.
    private static IFontHandle Create(float scale) =>
        Plugin.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
            e.OnPreBuild(toolkit =>
            {
                var sizePx = UiBuilder.DefaultFontSizePx * scale;
                var font = toolkit.AddDalamudDefaultFont(sizePx);
                toolkit.AddFontAwesomeIconFont(new SafeFontConfig { SizePx = sizePx, MergeFont = font });
            }));
}

/// <summary>The modifier that skips purchase confirmations (Daily Routines calls it the conflict key).</summary>
public sealed class PluginConfig
{
    private static readonly PluginConfig Shared = new();

    public static PluginConfig Instance() => Shared;

    public VirtualKey ConflictKeyBinding => VirtualKey.SHIFT;
}

/// <summary>
/// Sequential task queue run on framework ticks. A task returning <c>true</c> is done; <c>false</c> or <c>null</c>
/// retries next tick until it times out, which aborts the queue.
/// </summary>
public sealed class TaskHelper : IDisposable
{
    private readonly LinkedList<QueuedTask> queue = new();
    private QueuedTask? current;
    private long nextRunAt;

    public TaskHelper()
    {
        Plugin.Framework.Update += OnUpdate;
    }

    public int TaskIntervalMS { get; set; }

    public int TimeoutMS { get; set; } = 10_000;

    public bool IsBusy => current != null || queue.Count > 0;

    public void Enqueue(Func<bool> task, string? name = null, int timeoutMS = 0, int weight = 0) =>
        Add(new QueuedTask(() => task(), name, timeoutMS, weight));

    public void Enqueue(Func<bool?> task, string? name = null, int timeoutMS = 0, int weight = 0) =>
        Add(new QueuedTask(() => task() ?? false, name, timeoutMS, weight));

    public void Enqueue(Action task, string? name = null, int timeoutMS = 0, int weight = 0) =>
        Add(new QueuedTask(() =>
        {
            task();
            return true;
        }, name, timeoutMS, weight));

    public void Abort()
    {
        queue.Clear();
        current = null;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        Abort();
    }

    // Higher weight runs first; equal weights keep insertion order.
    private void Add(QueuedTask task)
    {
        for (var node = queue.First; node != null; node = node.Next)
        {
            if (node.Value.Weight < task.Weight)
            {
                queue.AddBefore(node, task);
                return;
            }
        }

        queue.AddLast(task);
    }

    private void OnUpdate(IFramework framework)
    {
        var now = Environment.TickCount64;
        if (now < nextRunAt)
            return;

        if (current == null)
        {
            if (queue.First is not { } first)
                return;
            current = first.Value;
            queue.RemoveFirst();
            current.Started.Restart();
        }

        var task = current;
        var timeout = task.TimeoutMs > 0 ? task.TimeoutMs : TimeoutMS;
        if (timeout > 0 && task.Started.ElapsedMilliseconds > timeout)
        {
            Plugin.Log.Warning($"BetterMarketBoard task {task.Name ?? "<unnamed>"} timed out; queue aborted.");
            Abort();
            return;
        }

        try
        {
            if (!task.Run())
                return;
            if (current == task)
                current = null;
            nextRunAt = now + TaskIntervalMS;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"BetterMarketBoard task {task.Name ?? "<unnamed>"} failed; queue aborted.");
            Abort();
        }
    }

    private sealed record QueuedTask(Func<bool> Run, string? Name, int TimeoutMs, int Weight)
    {
        public Stopwatch Started { get; } = new();
    }
}

/// <summary>Calls a Dalamud IPC function if the providing plugin is loaded.</summary>
public sealed class IPCSubscriber<T1, TRet>
{
    private readonly string name;

    public IPCSubscriber(string name)
    {
        this.name = name;
    }

    public TRet? InvokeFunc(T1 arg) => TryInvoke(arg, out var result) ? result : default;

    public bool TryInvoke(T1 arg, out TRet? result)
    {
        try
        {
            result = Plugin.PluginInterface.GetIpcSubscriber<T1, TRet>(name).InvokeFunc(arg);
            return true;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }
}

/// <summary>Static access to Dalamud's SeString evaluator, as the module expects.</summary>
public static class ISeStringEvaluator
{
    public static Dalamud.Plugin.Services.ISeStringEvaluator Instance() => Plugin.SeStringEvaluator;
}

public static class SeStringConversions
{
    public static SeString ToDalamud(this ReadOnlySeString text) => SeString.Parse(text.Data.Span);

    public static ReadOnlySeString ToReadOnly(this SeString text) => new(text.Encode());

    extension(VirtualKey key)
    {
        public bool IsPressed() => Plugin.KeyState[key];
    }
}
