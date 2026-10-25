// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi: Lumina lookups, game state queries and small game-memory helpers.
// SPDX-License-Identifier: AGPL-3.0-only

using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using TerritoryIntendedUseEnum = FFXIVClientStructs.FFXIV.Client.Enums.TerritoryIntendedUse;

namespace Pipi.Features.BetterMarketBoard.Compat;

public static class LuminaGetter
{
    public static ExcelSheet<T> Get<T>() where T : struct, IExcelRow<T> => Plugin.DataManager.GetExcelSheet<T>();

    public static bool TryGetRow<T>(uint rowId, out T row) where T : struct, IExcelRow<T> => Get<T>().TryGetRow(rowId, out row);

    public static T? GetRow<T>(uint rowId) where T : struct, IExcelRow<T> => Get<T>().GetRowOrDefault(rowId);

    public static T GetRowOrDefault<T>(uint rowId) where T : struct, IExcelRow<T> => GetRow<T>(rowId) ?? default;
}

public static class LuminaWrapper
{
    public static string GetAddonText(uint rowId) =>
        LuminaGetter.TryGetRow<Addon>(rowId, out var row) ? row.Text.ToString() : string.Empty;

    public static string GetWorldName(uint worldId) =>
        LuminaGetter.TryGetRow<World>(worldId, out var row) ? row.Name.ToString() : string.Empty;

    public static string GetItemName(uint itemId) =>
        LuminaGetter.TryGetRow<Item>(itemId, out var row) ? row.Name.ToString() : string.Empty;

    public static string GetPlaceName(uint rowId) =>
        LuminaGetter.TryGetRow<PlaceName>(rowId, out var row) ? row.Name.ToString() : string.Empty;
}

/// <summary>Case-insensitive substring search over a fixed data set using several key selectors.</summary>
public sealed class LuminaSearcher<T>
{
    private const int MaxResults = 500;

    private readonly Func<T, string>[] keySelectors;
    private readonly string[][] keys;

    public LuminaSearcher(IReadOnlyList<T> data, IReadOnlyList<Func<T, string>> keySelectors)
    {
        Data = data.ToList();
        this.keySelectors = keySelectors.ToArray();
        keys = Data.Select(item => this.keySelectors.Select(selector => selector(item)).ToArray()).ToArray();
    }

    public List<T> Data { get; }

    /// <summary>Replaced (never mutated) on every search, so callers can cache by reference.</summary>
    public List<T> SearchResult { get; private set; } = [];

    public void Search(string input)
    {
        input = input.Trim();
        if (input.Length == 0)
        {
            SearchResult = [];
            return;
        }

        var exact = new List<T>();
        var prefix = new List<T>();
        var contains = new List<T>();
        for (var i = 0; i < Data.Count; i++)
        {
            var bucket = Match(keys[i], input);
            if (bucket == 0)
                exact.Add(Data[i]);
            else if (bucket == 1)
                prefix.Add(Data[i]);
            else if (bucket == 2)
                contains.Add(Data[i]);
        }

        SearchResult = [.. exact.Concat(prefix).Concat(contains).Take(MaxResults)];
    }

    // 0 = exact, 1 = prefix, 2 = contains, 3 = no match.
    private static int Match(string[] candidates, string input)
    {
        var best = 3;
        foreach (var candidate in candidates)
        {
            if (candidate.Equals(input, StringComparison.OrdinalIgnoreCase))
                return 0;
            if (candidate.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                best = Math.Min(best, 1);
            else if (candidate.Contains(input, StringComparison.OrdinalIgnoreCase))
                best = Math.Min(best, 2);
        }

        return best;
    }
}

/// <summary>Player/world state plus detection of market board requests the server refused.</summary>
public sealed unsafe class GameState : IDisposable
{
    private static readonly GameState Shared = new();

    private delegate void ProcessRequestResultDelegate(InfoProxyItemSearch* proxy, byte resultCount, int errorCode);

    private Hook<ProcessRequestResultDelegate>? processRequestResultHook;

    public static GameState Instance() => Shared;

    public static uint CurrentWorld => Plugin.PlayerState.IsLoaded ? Plugin.PlayerState.CurrentWorld.RowId : 0;

    public static World CurrentWorldData => LuminaGetter.GetRowOrDefault<World>(CurrentWorld);

    public static bool IsLoggedIn => Plugin.ClientState.IsLoggedIn && Plugin.PlayerState.IsLoaded;

    public static uint ContentFinderCondition
    {
        get
        {
            var gameMain = GameMain.Instance();
            return gameMain == null ? 0 : (uint)gameMain->CurrentContentFinderConditionId;
        }
    }

    public static TerritoryIntendedUseEnum TerritoryIntendedUse =>
        LuminaGetter.TryGetRow<TerritoryType>(Plugin.ClientState.TerritoryType, out var territory)
            ? (TerritoryIntendedUseEnum)territory.TerritoryIntendedUse.RowId
            : default;

    /// <summary>True after the server rejected the last market board request (e.g. rate limited).</summary>
    public bool IsMarketListingsStuck { get; private set; }

    internal void Initialize()
    {
        if (processRequestResultHook != null)
            return;

        try
        {
            processRequestResultHook = Plugin.GameInteropProvider.HookFromAddress<ProcessRequestResultDelegate>(
                InfoProxyItemSearch.Addresses.ProcessRequestResult.Value,
                ProcessRequestResultDetour);
            processRequestResultHook.Enable();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Failed to hook InfoProxyItemSearch.ProcessRequestResult; stuck detection disabled.");
        }
    }

    public void Dispose()
    {
        processRequestResultHook?.Dispose();
        processRequestResultHook = null;
    }

    private void ProcessRequestResultDetour(InfoProxyItemSearch* proxy, byte resultCount, int errorCode)
    {
        processRequestResultHook!.Original(proxy, resultCount, errorCode);
        IsMarketListingsStuck = resultCount == 0 && errorCode > 0 && proxy->SearchItemId != 0;
        if (IsMarketListingsStuck)
            Plugin.Log.Warning($"Market board request rejected by the server, error 0x{errorCode:X}.");
    }
}

/// <summary>Condition lookup with the "busy in an event" shorthand the module uses.</summary>
public sealed class ICondition
{
    private static readonly ICondition Shared = new();

    public static ICondition Instance() => Shared;

    public bool this[ConditionFlag flag] => Plugin.Condition[flag];

    public bool IsOccupiedInEvent =>
        Plugin.Condition[ConditionFlag.OccupiedInEvent] ||
        Plugin.Condition[ConditionFlag.OccupiedInQuestEvent] ||
        Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent] ||
        Plugin.Condition[ConditionFlag.OccupiedSummoningBell];
}

public static unsafe class LocalPlayerState
{
    /// <summary>Normal-quality plus high-quality copies in the player's inventory.</summary>
    public static uint GetItemCount(uint itemId)
    {
        var inventory = InventoryManager.Instance();
        if (inventory == null)
            return 0;
        return (uint)(inventory->GetInventoryItemCount(itemId) + inventory->GetInventoryItemCount(itemId, true));
    }
}

public sealed class StandardTimeManager
{
    private static readonly StandardTimeManager Shared = new();

    public static StandardTimeManager Instance() => Shared;

    public DateTime Now => DateTime.Now;
}

/// <summary>Globals the module reaches for without qualification (imported with <c>using static</c>).</summary>
public static unsafe class Globals
{
    public static float GlobalUIScale => ImGuiHelpers.GlobalScale;

    public static Vector2 ScaledVector2(float x, float? y = null) => new Vector2(x, y ?? x) * ImGuiHelpers.GlobalScale;

    public static AtkUnitBase* ItemSearchResult => (AtkUnitBase*)Plugin.GameGui.GetAddonByName("ItemSearchResult").Address;

    public static AtkUnitBase* ScreenText => (AtkUnitBase*)Plugin.GameGui.GetAddonByName("_ScreenText").Address;

    public static AtkUnitBase* ItemDetail => (AtkUnitBase*)Plugin.GameGui.GetAddonByName("ItemDetail").Address;
}

public static unsafe class GameMemoryExtensions
{
    extension(ref AtkUnitBase addon)
    {
        public bool IsAddonAndNodesReady()
        {
            fixed (AtkUnitBase* ptr = &addon)
                return ptr != null && ptr->IsVisible && ptr->UldManager.LoadedState == AtkLoadState.Loaded && ptr->RootNode != null;
        }
    }

    extension(ref InfoProxyItemSearch proxy)
    {
        /// <summary>
        /// Whether every listing for the searched item has arrived. The server sends listings in pages of ten,
        /// and a purchase only shifts entries down, so only the counted range is checked.
        /// Check adapted from OmenTools' InfoProxyItemSearchExtension (MIT, AtmoOmen).
        /// </summary>
        public bool IsFullyReceived(uint itemId = 0)
        {
            if (GameState.Instance().IsMarketListingsStuck)
                return false;
            if (itemId != 0 && proxy.SearchItemId != itemId)
                return false;

            var listings = proxy.Listings;
            var count = (int)proxy.ListingCount;
            if (count > listings.Length)
                return false;

            for (var i = 0; i < count; i++)
            {
                if (listings[i].ItemId != proxy.SearchItemId || listings[i].UnitPrice == 0)
                    return false;
            }

            return proxy.EntryCount switch
            {
                0 => true,
                > 10 => count >= 10,
                _ => count != 0,
            };
        }
    }
}
