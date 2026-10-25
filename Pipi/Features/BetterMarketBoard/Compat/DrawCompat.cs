// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi: colour conversions, glow drawing, native item tooltips and small static entry points.
// SPDX-License-Identifier: AGPL-3.0-only

using System.Drawing;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaSeStringBuilder = Lumina.Text.SeStringBuilder;

namespace Pipi.Features.BetterMarketBoard.Compat;

public static class ColorExtensions
{
    public static uint ToUInt(this Vector4 color) => ImGui.ColorConvertFloat4ToU32(color);

    public static uint ToUInt(this KnownColor color) => color.ToVector4().ToUInt();

    /// <summary>ImGui packed ABGR → RGBA vector.</summary>
    public static Vector4 ToVector4(this uint color) => ImGui.ColorConvertU32ToFloat4(color);
}

public static class GlowExtensions
{
    extension(ImGuiOm)
    {
        /// <summary>Soft glow around a rect, built from <paramref name="layers"/> expanding translucent outlines.</summary>
        public static void AddGlowRect(
            ImDrawListPtr drawList, Vector2 min, Vector2 max, uint color, float rounding, float size, int layers, float strength = 0.35f)
        {
            if (size <= 0 || layers <= 0)
                return;

            var baseColor = ImGui.ColorConvertU32ToFloat4(color);
            for (var i = 1; i <= layers; i++)
            {
                var t = i / (float)layers;
                var spread = size * t;
                var alpha = baseColor.W * strength * (1f - t);
                if (alpha <= 0.001f)
                    continue;

                drawList.AddRect(
                    min - new Vector2(spread),
                    max + new Vector2(spread),
                    ImGui.ColorConvertFloat4ToU32(baseColor with { W = alpha }),
                    rounding + spread,
                    ImDrawFlags.RoundCornersAll,
                    Math.Max(1f, size / layers));
            }
        }
    }
}

/// <summary>Shows the game's own item tooltip anchored to a node (the module anchors it to <c>_ScreenText</c>).</summary>
public static unsafe class NativeTooltipExtensions
{
    extension(ref AtkStage stage)
    {
        public void ShowItemTooltip(AtkResNode* anchor, uint itemId)
        {
            var screenText = Globals.ScreenText;
            if (anchor == null || screenText == null)
                return;

            var args = new AtkTooltipManager.AtkTooltipArgs();
            args.Ctor();
            args.ItemArgs.ItemId = (int)itemId;
            stage.TooltipManager.ShowTooltip(AtkTooltipType.Item, screenText->Id, anchor, &args, null, false, true);
        }

        public void HideTooltip(ushort parentId) => stage.TooltipManager.HideTooltip(parentId, false);
    }

    extension(InfoProxyItemSearch)
    {
        public static bool IsListingsStuck => GameState.Instance().IsMarketListingsStuck;
    }
}

public static class SeStringBuilderExtensions
{
    /// <summary>Appends a Dalamud payload (e.g. a link) to a Lumina SeString builder.</summary>
    public static LuminaSeStringBuilder AppendDalamudSeString(this LuminaSeStringBuilder builder, Payload payload) =>
        builder.Append(new Lumina.Text.ReadOnly.ReadOnlySeString(payload.Encode()));

    public static LuminaSeStringBuilder AppendDalamudSeString(this LuminaSeStringBuilder builder, SeString text) =>
        builder.Append(new Lumina.Text.ReadOnly.ReadOnlySeString(text.Encode()));
}

/// <summary>Static access to Dalamud's texture provider, as the module expects.</summary>
public static class ITextureProvider
{
    public static Dalamud.Plugin.Services.ITextureProvider Instance() => Plugin.TextureProvider;
}
