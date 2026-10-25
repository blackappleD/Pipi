// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi: ImGui helpers and small extension methods the module calls.
// SPDX-License-Identifier: AGPL-3.0-only

using System.Drawing;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace Pipi.Features.BetterMarketBoard.Compat;

public static class ImGuiOm
{
    private static string? holdingId;
    private static long holdStartedAt;
    private static bool holdFired;

    /// <summary>Shows <paramref name="text"/> as a tooltip while the last item is hovered.</summary>
    public static void TooltipHover(string text, float wrapWidth = 400f)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) || string.IsNullOrEmpty(text))
            return;

        using var tooltip = ImRaii.Tooltip();
        ImGui.PushTextWrapPos(wrapWidth * Globals.GlobalUIScale);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    /// <summary>Copies <paramref name="text"/> when the last item is clicked.</summary>
    public static void ClickToCopyAndNotify(string text)
    {
        if (!ImGui.IsItemClicked())
            return;
        ImGui.SetClipboardText(text);
        NotifyHelper.Instance().NotificationSuccess($"{Lang.Get("CopiedToClipboard")}: {text}");
    }

    public static void HelpMarker(string text)
    {
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
        TooltipHover(text);
    }

    public static void TextCentered(string text)
    {
        var width = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (ImGui.GetContentRegionAvail().X - width) / 2));
        ImGui.TextUnformatted(text);
    }

    public static bool TreeNodeImageWithText(ImTextureID texture, Vector2 imageSize, string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    {
        var visibleLabel = label.Split("##")[0];
        var open = ImGui.TreeNodeEx($"##{label}", flags | ImGuiTreeNodeFlags.AllowItemOverlap);
        ImGui.SameLine();
        ImGui.Image(texture, imageSize);
        ImGui.SameLine();
        ImGui.TextUnformatted(visibleLabel);
        return open;
    }

    /// <summary>A button that returns true once after being held for <paramref name="holdMs"/>.</summary>
    public static bool HoldButton(string id, string label, string? tooltip = null, bool showProgress = true, Vector2? size = null, int holdMs = 1000)
    {
        ImGui.Button($"{label}{id}", size ?? Vector2.Zero);
        if (tooltip != null)
            TooltipHover(tooltip);

        if (!ImGui.IsItemActive())
        {
            if (holdingId == id)
            {
                holdingId = null;
                holdFired = false;
            }

            return false;
        }

        var now = Environment.TickCount64;
        if (holdingId != id)
        {
            holdingId = id;
            holdStartedAt = now;
            holdFired = false;
        }

        var progress = Math.Clamp((now - holdStartedAt) / (float)holdMs, 0f, 1f);
        if (showProgress)
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddRectFilled(
                min,
                new Vector2(min.X + (max.X - min.X) * progress, max.Y),
                ImGui.GetColorU32(ImGuiColors.DalamudOrange with { W = 0.35f }),
                ImGui.GetStyle().FrameRounding);
        }

        if (progress < 1f || holdFired)
            return false;

        holdFired = true;
        return true;
    }
}

public static class CompatExtensions
{
    public static Vector4 ToVector4(this KnownColor color)
    {
        var c = Color.FromKnownColor(color);
        return new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    }

    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> factory)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            value = factory(key);
            dictionary[key] = value;
        }

        return value;
    }

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
            action(item);
    }

    /// <summary>Groups digits by 10⁴ with 万/亿 units, e.g. 12345678 → "1234万5678".</summary>
    public static string ToChineseString(this ulong value)
    {
        if (value < 10_000)
            return value.ToString(CultureInfo.InvariantCulture);

        var yi = value / 100_000_000;
        var wan = value / 10_000 % 10_000;
        var rest = value % 10_000;
        var text = yi > 0 ? $"{yi}亿" : string.Empty;
        if (wan > 0)
            text += yi > 0 ? $"{wan:0000}万" : $"{wan}万";
        else if (yi > 0 && rest > 0)
            text += "0000万";
        if (rest > 0)
            text += $"{rest:0000}";
        return text;
    }

    public static string ToChineseString(this uint value) => ((ulong)value).ToChineseString();

    public static string ToChineseString(this int value) => value < 0 ? $"-{((ulong)(-(long)value)).ToChineseString()}" : ((ulong)value).ToChineseString();

    public static string ToChineseString(this long value) => value < 0 ? $"-{((ulong)(-value)).ToChineseString()}" : ((ulong)value).ToChineseString();

    /// <summary>Relative time in Chinese, e.g. "5 分钟前".</summary>
    public static string TimeAgo(this DateTime time)
    {
        var delta = DateTime.Now - (time.Kind == DateTimeKind.Utc ? time.ToLocalTime() : time);
        if (delta < TimeSpan.Zero)
            delta = TimeSpan.Zero;
        if (delta < TimeSpan.FromMinutes(1))
            return "刚刚";
        if (delta < TimeSpan.FromHours(1))
            return $"{(int)delta.TotalMinutes} 分钟前";
        if (delta < TimeSpan.FromDays(1))
            return $"{(int)delta.TotalHours} 小时前";
        if (delta < TimeSpan.FromDays(30))
            return $"{(int)delta.TotalDays} 天前";
        if (delta < TimeSpan.FromDays(365))
            return $"{(int)(delta.TotalDays / 30)} 个月前";
        return $"{(int)(delta.TotalDays / 365)} 年前";
    }

    extension(ReadOnlySeString)
    {
        /// <summary>An item name wrapped in an item link.</summary>
        public static ReadOnlySeString CreateItem(uint itemId, bool hq = false)
        {
            var linkId = hq ? itemId + 1_000_000 : itemId;
            var name = LuminaWrapper.GetItemName(itemId) + (hq ? " " : string.Empty);
            var builder = new SeStringBuilder();
            builder.PushLinkItem(linkId, name);
            builder.Append(name);
            builder.PopLink();
            return builder.ToReadOnlySeString();
        }
    }
}
