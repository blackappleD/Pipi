// Ported from the Daily Routines "BetterMarketBoard" module (authors: Fragile, AtmoOmen),
// https://github.com/Dalamud-DailyRoutines/DailyRoutines.ModulesPublic @ 7569a18, licensed under the GNU AGPL v3.
// Modified for Pipi by blackappleD, 2026-10-25: namespace/using changes; Daily Routines framework calls are
// served by Pipi's compatibility layer in Compat/; Daily Routines IPC providers removed.
// SPDX-License-Identifier: AGPL-3.0-only

#nullable disable warnings

using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using Dalamud.Game;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using Lumina.Excel.Sheets;
using Pipi.Features.BetterMarketBoard.Compat;
using System.Drawing;
using System.Numerics;
using IFramework = Dalamud.Plugin.Services.IFramework;
using static Pipi.Features.BetterMarketBoard.Compat.Globals;

namespace Pipi.Features.BetterMarketBoard;

public partial class BetterMarketBoard
{
    private readonly record struct MetricItem
    (
        string  Label,
        string  Value,
        Vector4 ValueColor,
        string? Tooltip
    );

    private readonly record struct WorldPriceRow
    (
        uint   WorldID,
        string WorldName,
        ulong  MinPrice
    );

    private readonly record struct RankedWorldPriceRow
    (
        string DCName,
        uint   WorldID,
        string WorldName,
        ulong  MinPrice
    );

    private readonly record struct HistoryEntry
    (
        double   X,
        DateTime SaleTime,
        ulong    PricePerUnit,
        uint     Quantity,
        bool     IsHQ
    );

    private class Config : ModuleConfig
    {
        public Dictionary<string, Dictionary<string, Dictionary<uint, string>>> AllWorlds = [];

        public bool AutoBuy;
        public uint AutoBuyQuantityLimit;

        public MonitorItem? CurrentMonitorItem;

        public Dictionary<uint, MarketFavoriteItem> FavoriteItems = [];
        public Dictionary<uint, MarketHistoryItem>  HistoryItems  = [];

        public bool AppendMarketStatsTooltip = true;
    }

    private enum ItemSelectorTab
    {
        Search,
        History,
        Favorite
    }

    private class MarketHistoryItem
    {
        public uint     ItemID     { get; set; }
        public DateTime AccessTime { get; set; }

        public Item GetData() =>
            LuminaGetter.GetRow<Item>(ItemID).GetValueOrDefault();
    }

    private class MarketFavoriteItem
    {
        public uint   ItemID { get; set; }
        public string Note   { get; set; } = string.Empty;

        public Item GetData() =>
            LuminaGetter.GetRow<Item>(ItemID).GetValueOrDefault();
    }

    private class MonitorItem
    {
        public uint ItemID         { get; set; }
        public uint PriceThreshold { get; set; } = 100;
        public bool HQOnly         { get; set; }

        public Item GetData() =>
            LuminaGetter.GetRow<Item>(ItemID).GetValueOrDefault();
    }
}
