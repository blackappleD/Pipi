// Ported from the Daily Routines "BetterMarketBoard" module (authors: Fragile, AtmoOmen),
// https://github.com/Dalamud-DailyRoutines/DailyRoutines.ModulesPublic @ 7569a18, licensed under the GNU AGPL v3.
// Modified for Pipi by blackappleD, 2026-10-25: namespace/using changes; Daily Routines framework calls are
// served by Pipi's compatibility layer in Compat/; Daily Routines IPC providers removed.
// SPDX-License-Identifier: AGPL-3.0-only

#nullable disable warnings

using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.Game;
using Pipi.Features.BetterMarketBoard.Compat;
using System.Drawing;
using IFramework = Dalamud.Plugin.Services.IFramework;
using static Pipi.Features.BetterMarketBoard.Compat.Globals;

namespace Pipi.Features.BetterMarketBoard;

public unsafe partial class BetterMarketBoard
{
    private static bool IsAbleToSearchMarket() =>
        IsAbleToSearchLocalMarket() &&
        (!ICondition.Instance().IsOccupiedInEvent ||
         ICondition.Instance()[ConditionFlag.OccupiedSummoningBell]) &&
        !ItemSearchResult->IsAddonAndNodesReady();

    private static bool IsAbleToSearchLocalMarket() =>
        GameState.IsLoggedIn                  &&
        GameState.ContentFinderCondition == 0 &&
        !ICondition.Instance()[ConditionFlag.OnFreeTrial];

    private static bool IsOwnRetainer
    (
        ulong retainerID
    )
    {
        var manager = RetainerManager.Instance();

        if (manager == null) return false;

        for (var i = 0U; i < manager->GetRetainerCount(); i++)
        {
            var retainer = manager->GetRetainerBySortedIndex(i);
            if (retainer != null && retainer->RetainerId == retainerID)
                return true;
        }

        return false;
    }
}
