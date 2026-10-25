// Copied from Daily Routines Common (https://github.com/Dalamud-DailyRoutines/DailyRoutines.Common @ 2d1b52e),
// licensed under the GNU AGPL v3. Modified for Pipi by blackappleD, 2026-10-25: namespace/using changes only.
// SPDX-License-Identifier: AGPL-3.0-only

using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using Dalamud.Game;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using Pipi.Features.BetterMarketBoard.Compat;
using System.Drawing;
using IFramework = Dalamud.Plugin.Services.IFramework;
using static Pipi.Features.BetterMarketBoard.Compat.Globals;

namespace Pipi.Features.BetterMarketBoard.Compat;

public enum CardCursorAdvance
{
    Bottom,
    Right,
    None
}
