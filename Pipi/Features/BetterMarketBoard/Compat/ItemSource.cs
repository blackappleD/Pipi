// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi: the module only asks which NPCs sell an item for gil, answered from the GilShopItem sheet.
// SPDX-License-Identifier: AGPL-3.0-only

using Lumina.Excel.Sheets;

namespace Pipi.Features.BetterMarketBoard.Compat;

public enum ItemSourceQueryState
{
    Ready,
    NotFound,
}

public sealed record ItemSourceCostInfo(uint ItemID, uint Cost);

public sealed record ItemSourceNpcInfo(IReadOnlyList<ItemSourceCostInfo> CostInfos);

public sealed record ItemSourceQueryResult(ItemSourceQueryState State, ItemSourceInfo? Data);

public sealed class ItemSourceInfo
{
    private const uint GilItemId = 1;

    private static HashSet<uint>? gilShopItems;

    public required uint ItemID { get; init; }

    public IReadOnlyList<ItemSourceNpcInfo> NPCInfos { get; init; } = [];

    public static ItemSourceQueryResult Query(uint itemId)
    {
        gilShopItems ??= BuildGilShopItems();
        if (!gilShopItems.Contains(itemId) || !LuminaGetter.TryGetRow<Item>(itemId, out var item) || item.PriceMid == 0)
            return new ItemSourceQueryResult(ItemSourceQueryState.Ready, new ItemSourceInfo { ItemID = itemId });

        return new ItemSourceQueryResult(
            ItemSourceQueryState.Ready,
            new ItemSourceInfo
            {
                ItemID = itemId,
                NPCInfos = [new ItemSourceNpcInfo([new ItemSourceCostInfo(GilItemId, item.PriceMid)])],
            });
    }

    private static HashSet<uint> BuildGilShopItems()
    {
        var result = new HashSet<uint>();
        foreach (var shop in Plugin.DataManager.GetSubrowExcelSheet<GilShopItem>())
        {
            foreach (var entry in shop)
                result.Add(entry.Item.RowId);
        }

        return result;
    }
}
