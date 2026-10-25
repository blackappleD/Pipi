// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi: lets the module append text to the game's item tooltip (the ItemDetail addon).
// SPDX-License-Identifier: AGPL-3.0-only

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace Pipi.Features.BetterMarketBoard.Compat;

/// <summary>Index into the ItemDetail string array.</summary>
public enum TooltipItemType
{
    Description = 13,
}

public enum TooltipModificationType
{
    Append,
    Replace,
}

public sealed class TooltipItemModification
{
    public TooltipItemType Target { get; init; }

    public TooltipModificationType Type { get; init; }

    public ReadOnlySeString Text { get; init; }
}

public delegate void ItemTooltipUpdateDelegate(ItemKind kind, uint itemID, ref List<TooltipItemModification> modifications);

/// <summary>
/// Applies registered modifications after the game (and any other plugin's PreRequestedUpdate listener, e.g. Daily
/// Routines' own tooltip manager, which rebuilds the description from its cached original) has filled the tooltip,
/// then re-runs the tooltip layout. Working in PostRequestedUpdate makes the result independent of listener order.
/// The text we started from is remembered per field so repeated updates never stack appended text.
/// </summary>
public sealed unsafe class TooltipManager : IDisposable
{
    private static readonly TooltipManager Shared = new();

    // ItemDetail number array: index of the field visibility flags, and the flag that shows the description field
    // (per Allagan Tools' ItemTooltipFieldVisibility).
    private const int FieldVisibilityIndex = 5;
    private const int DescriptionVisibleFlag = 2;

    private readonly List<ItemTooltipUpdateDelegate> handlers = [];
    private readonly Dictionary<int, (byte[] Original, byte[] Written)> fieldCache = [];
    private uint cachedItemId;
    private bool registered;

    public static TooltipManager Instance() => Shared;

    public void RegItem(ItemTooltipUpdateDelegate handler) => handlers.Add(handler);

    public void Unreg(ItemTooltipUpdateDelegate handler) => handlers.Remove(handler);

    /// <summary>Asks the game to rebuild the open item tooltip, e.g. after new market data arrived.</summary>
    public void TriggerItemDetailUpdate() =>
        Plugin.Framework.RunOnFrameworkThread(() =>
        {
            var addon = Globals.ItemDetail;
            if (addon == null || !addon->IsVisible)
                return;
            var agent = AgentItemDetail.Instance();
            if (agent != null)
                agent->IsDirty = true;
        });

    internal void Initialize()
    {
        if (registered)
            return;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", OnItemDetailUpdate);
        registered = true;
    }

    public void Dispose()
    {
        if (registered)
            Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "ItemDetail", OnItemDetailUpdate);
        registered = false;
        handlers.Clear();
    }

    private void OnItemDetailUpdate(AddonEvent type, AddonArgs args)
    {
        if (handlers.Count == 0)
            return;

        var addon = (AddonItemDetail*)args.Addon.Address;
        var agent = AgentItemDetail.Instance();
        var stage = AtkStage.Instance();
        var strings = stage->GetStringArrayData(StringArrayType.ItemDetail);
        var numbers = stage->GetNumberArrayData(NumberArrayType.ItemDetail);
        if (addon == null || agent == null || strings == null || numbers == null)
            return;

        var rawId = agent->ItemId;
        if (rawId != cachedItemId)
        {
            cachedItemId = rawId;
            fieldCache.Clear();
        }

        var itemId = ItemIds.Normalize(rawId, out var kind);
        var modifications = new List<TooltipItemModification>();
        foreach (var handler in handlers.ToArray())
        {
            try
            {
                handler(kind, itemId, ref modifications);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Item tooltip handler failed");
            }
        }

        var changed = false;
        foreach (var group in modifications.GroupBy(m => (int)m.Target))
            changed |= ApplyField(strings, group.Key, group);

        // When the item's description is empty the game hides the field, so the appended text would not show.
        if (modifications.Any(m => m.Target == TooltipItemType.Description))
            changed |= EnsureFieldVisible(numbers, DescriptionVisibleFlag);

        if (!changed)
            return;

        // The game already laid the tooltip out from the unmodified strings: refill the nodes from ours, then
        // re-stack the groups (this also resizes the window), and keep the now taller tooltip on screen.
        addon->GenerateItemTooltip(numbers, strings);
        addon->UpdateGroupPositions(numbers, strings);
        KeepOnScreen((AtkUnitBase*)addon);
    }

    private static bool EnsureFieldVisible(NumberArrayData* numbers, int flag)
    {
        if (FieldVisibilityIndex >= numbers->AtkArrayData.Size)
            return false;

        var flags = numbers->IntArray[FieldVisibilityIndex];
        if ((flags & flag) != 0)
            return false;

        // Written directly rather than via SetValue, so no array-changed notification triggers another update.
        numbers->IntArray[FieldVisibilityIndex] = flags | flag;
        return true;
    }

    private static void KeepOnScreen(AtkUnitBase* addon)
    {
        var device = Device.Instance();
        if (device == null)
            return;

        var height = addon->GetScaledHeight(true);
        var bottom = addon->Y + height;
        if (bottom <= device->Height)
            return;

        addon->SetPosition(addon->X, (short)Math.Max(0, device->Height - height));
    }

    private bool ApplyField(StringArrayData* strings, int index, IEnumerable<TooltipItemModification> modifications)
    {
        if (index < 0 || index >= strings->AtkArrayData.Size)
            return false;

        var current = ReadField(strings, index);
        // If the field still holds what we wrote last time, the game did not regenerate it: start from the cached original.
        var original = fieldCache.TryGetValue(index, out var cached) && current.AsSpan().SequenceEqual(cached.Written)
            ? cached.Original
            : current;

        var builder = new SeStringBuilder();
        builder.Append(new ReadOnlySeStringSpan(original));
        var empty = original.Length == 0;
        foreach (var modification in modifications)
        {
            if (modification.Type == TooltipModificationType.Replace)
            {
                builder.Clear();
                empty = true;
            }
            else if (!empty)
            {
                // Appended blocks start on their own line below the existing text.
                builder.AppendNewLine();
            }

            builder.Append(modification.Text);
            empty = false;
        }

        var written = builder.ToArray();
        fieldCache[index] = (original, written);
        if (current.AsSpan().SequenceEqual(written))
            return false;

        // Suppress the array-changed notification: it would request another update and loop.
        strings->SetValue(index, written, false, true, true);
        return true;
    }

    private static byte[] ReadField(StringArrayData* strings, int index)
    {
        var pointer = strings->StringArray[index];
        return pointer.HasValue ? pointer.AsSpan().ToArray() : [];
    }
}
