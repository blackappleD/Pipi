// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi: Simplified Chinese UI strings for the keys the module looks up (wording by Pipi).
// SPDX-License-Identifier: AGPL-3.0-only

using Dalamud.Game.Text.SeStringHandling;
using SeStringBuilder = Lumina.Text.SeStringBuilder;
using Lumina.Text.ReadOnly;

namespace Pipi.Features.BetterMarketBoard.Compat;

public static class Lang
{
    private static readonly Dictionary<string, string> Strings = new()
    {
        ["All"] = "全部",
        ["Amount"] = "数量",
        ["Apply"] = "应用",
        ["Clear"] = "清空",
        ["Command"] = "指令",
        ["Confirm"] = "确认",
        ["CopiedToClipboard"] = "已复制",
        ["DeselectAll"] = "全不选",
        ["Disable"] = "关闭",
        ["Disabled"] = "已关闭",
        ["Enable"] = "开启",
        ["Enabled"] = "已开启",
        ["Favorite"] = "收藏",
        ["HQ"] = "HQ",
        ["Highest"] = "最高",
        ["History"] = "历史",
        ["InvertSelection"] = "反选",
        ["LeftClick"] = "左键",
        ["Lowest"] = "最低",
        ["Mannequin"] = "人偶",
        ["NQ"] = "NQ",
        ["PleaseSearch"] = "输入物品名或 ID 进行搜索",
        ["RightClick"] = "右键",
        ["Search"] = "搜索",
        ["SelectAll"] = "全选",
        ["Selected"] = "已选",
        ["Settings"] = "设置",
        ["Tax"] = "税",
        ["Unfavorite"] = "取消收藏",

        ["BetterMarketBoardTitle"] = "市场布告板",
        ["BetterMarketBoardDescription"] = "/pipi {0}：远程查看市场挂单、跨服比价并设置价格监控",
        ["BetterMarketBoard-CommandHelp"] = "[物品名/ID]：开关市场布告板（或直接查看指定物品）",
        ["BetterMarketBoard-SearchInMarket"] = "查看市场数据",
        ["BetterMarketBoard-Config-AppendMarketStatsTooltip"] = "在道具说明中显示市场数据",
        ["BetterMarketBoard-Config-AppendMarketStatsTooltip-Help"] = "可在市场交易的物品，其道具说明末尾会追加来自 Universalis 的价格与销量",

        ["BetterMarketBoard-MarketData"] = "市场数据",
        ["BetterMarketBoard-PriceTrend"] = "价格走势",
        ["BetterMarketBoard-MarketView-HQ"] = "仅 HQ",
        ["BetterMarketBoard-ReloadMarketData"] = "重新获取市场数据",
        ["BetterMarketBoard-TravelToWorld"] = "前往该服务器（需安装 Daily Routines）",
        ["BetterMarketBoard-Home"] = "当前服务器",
        ["BetterMarketBoard-OnlyOne"] = "仅一个服务器有挂单",
        ["BetterMarketBoard-NPCShopInfo"] = "NPC 商店",
        ["BetterMarketBoard-NPCPrice"] = "NPC 售价：{0}",

        ["BetterMarketBoard-InStock"] = "在售",
        ["BetterMarketBoard-Samples-Format"] = "{0} 条（共 {1} 个）",
        ["BetterMarketBoard-DailySales-Format"] = "{0} 个/天",
        ["BetterMarketBoard-Percent-Format"] = "{0}%",
        ["BetterMarketBoard-Turnover"] = "周转",
        ["BetterMarketBoard-Turnover-Format"] = "约 {0} 天",
        ["BetterMarketBoard-Turnover-Help"] = "在售数量 ÷ 日销量",
        ["BetterMarketBoard-DataFreshness"] = "数据时间",
        ["BetterMarketBoard-AveragePrice"] = "成交均价",
        ["BetterMarketBoard-AveragePrice-NQ"] = "NQ 均价",
        ["BetterMarketBoard-AveragePrice-HQ"] = "HQ 均价",
        ["BetterMarketBoard-HQRatio"] = "HQ 比例",
        ["BetterMarketBoard-History-Samples"] = "成交记录",
        ["BetterMarketBoard-History-Recent"] = "最近成交",
        ["BetterMarketBoard-RecentPurchase"] = "最近成交",

        ["BetterMarketBoard-BatchPurchase-Button"] = "按住购买所选",
        ["BetterMarketBoard-BatchPurchase-Total"] = "合计",
        ["BetterMarketBoard-Purchase-Help"] = "按住 {0} 时右键该行可直接购买",

        ["BetterMarketBoard-Trend-Min"] = "最低",
        ["BetterMarketBoard-Trend-Max"] = "最高",
        ["BetterMarketBoard-Trend-Avg"] = "均价",
        ["BetterMarketBoard-Trend-Volume"] = "成交量",

        ["BetterMarketBoard-Tooltip-CurrentWorld"] = "本服",
        ["BetterMarketBoard-Tooltip-CurrentRegion"] = "全区",
        ["BetterMarketBoard-Tooltip-DailySales"] = "日销量",

        ["BetterMarketBoard-PriceMonitor-ToMonitor"] = "监控价格",
        ["BetterMarketBoard-PriceMonitor-ToNotMonitor"] = "停止监控",
        ["BetterMarketBoard-PriceMonitor-Item"] = "监控物品",
        ["BetterMarketBoard-PriceMonitor-Price"] = "监控价",
        ["BetterMarketBoard-PriceMonitor-AutoPurchase"] = "自动购买",
        ["BetterMarketBoard-PriceMonitor-AutoPurchase-QuantityLimit"] = "持有上限",
        ["BetterMarketBoard-PriceMonitor-AutoPurchase-QuantityLimit-Help"] = "背包中持有数量达到此值后不再自动购买；0 表示不限",
        ["BetterMarketBoard-PriceMonitor-Quick-Title"] = "参考价",
        ["BetterMarketBoard-PriceMonitor-Quick-WorldMin"] = "本服最低",
        ["BetterMarketBoard-PriceMonitor-Quick-RegionMin"] = "全区最低",
        ["BetterMarketBoard-PriceMonitor-Quick-Average"] = "成交均价",
        ["BetterMarketBoard-PriceMonitor-Quick-Median"] = "中位数",
        ["BetterMarketBoard-PriceMonitor-Quick-LowPercentile"] = "低位价",
        ["BetterMarketBoard-PriceMonitor-Quick-NPC"] = "NPC 售价",
        ["BetterMarketBoard-PriceMonitor-Notification-Found"] = "{0} 本服最低价 {1}，已低于监控价 {2}",
        ["BetterMarketBoard-PriceMonitor-Notification-AutoBuy"] = "已为 {0} 提交自动购买：{1} 条挂单，共 {2} 个",
    };

    public static string Get(string key, params object?[] args)
    {
        if (!Strings.TryGetValue(key, out var template))
        {
            Plugin.Log.Debug($"BetterMarketBoard: missing string {key}");
            template = key;
        }

        return args.Length == 0 ? template : string.Format(template, args.Select(Display).ToArray());
    }

    /// <summary>Like <see cref="Get"/>, but arguments may be rich text (item links etc.) and are kept as payloads.</summary>
    public static ReadOnlySeString GetSe(string key, params object?[] args)
    {
        var template = Strings.GetValueOrDefault(key, key);
        var builder = new SeStringBuilder();
        var position = 0;
        while (position < template.Length)
        {
            var open = template.IndexOf('{', position);
            var close = open < 0 ? -1 : template.IndexOf('}', open);
            if (open < 0 || close < 0 || !int.TryParse(template.AsSpan(open + 1, close - open - 1), out var index))
            {
                builder.Append(template[position..]);
                break;
            }

            builder.Append(template[position..open]);
            if (index < args.Length)
                AppendArgument(builder, args[index]);
            position = close + 1;
        }

        return builder.ToReadOnlySeString();
    }

    private static void AppendArgument(SeStringBuilder builder, object? arg)
    {
        switch (arg)
        {
            case ReadOnlySeString text:
                builder.Append(text);
                break;
            case SeString text:
                builder.Append(new ReadOnlySeString(text.Encode()));
                break;
            default:
                builder.Append(Display(arg));
                break;
        }
    }

    private static string Display(object? arg) => arg switch
    {
        null => string.Empty,
        ReadOnlySeString text => text.ExtractText(),
        SeString text => text.TextValue,
        _ => arg.ToString() ?? string.Empty,
    };
}
