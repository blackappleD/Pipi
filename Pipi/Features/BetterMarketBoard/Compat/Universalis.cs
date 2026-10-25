// Pipi compatibility layer for the BetterMarketBoard module ported from Daily Routines.
// Written for Pipi against the public Universalis v2 REST API (https://docs.universalis.app/):
// cached snapshots of remote data plus change subscriptions delivered on the framework thread.
// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Game;

namespace Pipi.Features.BetterMarketBoard.Compat;

public enum RemoteSnapshotStatus
{
    Idle,
    Loading,
    Ready,
    Refreshing,
    Failed,
}

public readonly record struct RemoteSnapshot<T>(RemoteSnapshotStatus Status, T? Value, string? Error) where T : class
{
    public bool HasValue => Value != null;
}

#region Models

public sealed class UniversalisDataCenter
{
    public string Name { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public List<uint> Worlds { get; set; } = [];
}

public sealed class UniversalisWorld
{
    [JsonPropertyName("id")] public uint ID { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class UniversalisMarketListing
{
    public string? ListingID { get; set; }

    public uint PricePerUnit { get; set; }

    public uint Quantity { get; set; }

    public ulong Total { get; set; }

    public ulong Tax { get; set; }

    [JsonPropertyName("hq")] public bool HQ { get; set; }

    public bool OnMannequin { get; set; }

    public string? RetainerName { get; set; }

    public string? WorldName { get; set; }

    public uint? WorldID { get; set; }

    /// <summary>Unix seconds.</summary>
    public long LastReviewTime { get; set; }
}

public sealed class UniversalisMarketItemData
{
    public uint ItemID { get; set; }

    public uint? WorldID { get; set; }

    /// <summary>Unix milliseconds.</summary>
    public long LastUploadTime { get; set; }

    public List<UniversalisMarketListing>? Listings { get; set; }

    public DateTime GetLastUploadTime() => DateTimeOffset.FromUnixTimeMilliseconds(LastUploadTime).UtcDateTime;
}

public sealed class UniversalisMarketDataResponse
{
    public uint WorldID { get; init; }

    public Dictionary<uint, UniversalisMarketItemData> Items { get; init; } = [];
}

public sealed class UniversalisHistorySale
{
    public uint PricePerUnit { get; set; }

    public uint Quantity { get; set; }

    [JsonPropertyName("hq")] public bool HQ { get; set; }

    public bool OnMannequin { get; set; }

    public string? BuyerName { get; set; }

    public string? WorldName { get; set; }

    /// <summary>Unix seconds.</summary>
    public long Timestamp { get; set; }

    public DateTime GetSaleTime() => DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime;
}

public sealed class UniversalisMarketHistoryItem
{
    public uint ItemID { get; set; }

    public uint? WorldID { get; set; }

    public List<UniversalisHistorySale>? Entries { get; set; }
}

public sealed class UniversalisMarketHistoryResponse
{
    public uint WorldID { get; init; }

    public Dictionary<uint, UniversalisMarketHistoryItem> Items { get; init; } = [];
}

public sealed class UniversalisAggregatedPoint
{
    public double? Price { get; set; }

    public uint? WorldID { get; set; }

    /// <summary>Unix milliseconds.</summary>
    public long? Timestamp { get; set; }

    public float? Quantity { get; set; }
}

/// <summary>A statistic over the queried world, its data center and its region. Never null after parsing.</summary>
public sealed class UniversalisAggregatedScoped
{
    public UniversalisAggregatedPoint World { get; set; } = new();

    public UniversalisAggregatedPoint Dc { get; set; } = new();

    public UniversalisAggregatedPoint Region { get; set; } = new();

    internal void Normalize()
    {
        World ??= new UniversalisAggregatedPoint();
        Dc ??= new UniversalisAggregatedPoint();
        Region ??= new UniversalisAggregatedPoint();
    }
}

public sealed class UniversalisAggregatedMarketScope
{
    public UniversalisAggregatedScoped MinListing { get; set; } = new();

    public UniversalisAggregatedScoped RecentPurchase { get; set; } = new();

    public UniversalisAggregatedScoped AverageSalePrice { get; set; } = new();

    public UniversalisAggregatedScoped DailySaleVelocity { get; set; } = new();

    internal void Normalize()
    {
        MinListing ??= new UniversalisAggregatedScoped();
        RecentPurchase ??= new UniversalisAggregatedScoped();
        AverageSalePrice ??= new UniversalisAggregatedScoped();
        DailySaleVelocity ??= new UniversalisAggregatedScoped();
        MinListing.Normalize();
        RecentPurchase.Normalize();
        AverageSalePrice.Normalize();
        DailySaleVelocity.Normalize();
    }
}

public sealed class UniversalisAggregatedMarketResult
{
    [JsonPropertyName("itemId")] public uint ItemID { get; set; }

    [JsonPropertyName("nq")] public UniversalisAggregatedMarketScope NQ { get; set; } = new();

    [JsonPropertyName("hq")] public UniversalisAggregatedMarketScope HQ { get; set; } = new();
}

public sealed class UniversalisAggregatedMarketDataResponse
{
    public List<UniversalisAggregatedMarketResult> Results { get; set; } = [];

    internal void Normalize()
    {
        foreach (var result in Results)
        {
            result.NQ ??= new UniversalisAggregatedMarketScope();
            result.HQ ??= new UniversalisAggregatedMarketScope();
            result.NQ.Normalize();
            result.HQ.Normalize();
        }
    }
}

public sealed class UniversalisMarketDataRequestParams
{
    public bool HQ { get; init; }

    public int? Listings { get; init; }

    public int? Entries { get; init; }

    internal string Query()
    {
        var parts = new List<string>();
        if (HQ)
            parts.Add("hq=true");
        if (Listings is { } listings)
            parts.Add($"listings={listings}");
        if (Entries is { } entries)
            parts.Add($"entries={entries}");
        return string.Join('&', parts);
    }
}

public sealed class UniversalisMarketHistoryRequestParams
{
    public int EntriesToReturn { get; init; } = 200;

    /// <summary>Milliseconds.</summary>
    public long StatsWithin { get; init; }

    /// <summary>Milliseconds (Universalis expects seconds; converted on request).</summary>
    public long EntriesWithin { get; init; }

    internal string Query()
    {
        var parts = new List<string> { $"entriesToReturn={EntriesToReturn}" };
        if (StatsWithin > 0)
            parts.Add($"statsWithin={StatsWithin}");
        if (EntriesWithin > 0)
            parts.Add($"entriesWithin={EntriesWithin / 1000}");
        return string.Join('&', parts);
    }
}

#endregion

/// <summary>Shared HTTP plumbing: one slot per URL, refreshed after a TTL, with subscribers notified on change.</summary>
public static class Universalis
{
    private const string OfficialBaseUrl = "https://universalis.app/api/v2/";

    // Mirror operated by AtmoOmen (Daily Routines) for the Chinese client; same API, much faster from mainland China.
    private const string ChinaMirrorBaseUrl = "https://universalis.atmoomen.top/api/v2/";

    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Universalis allows ~8 concurrent connections per IP; the module asks for one aggregate per world at once.
    private const int MaxConcurrentRequests = 4;
    private const int MaxAttempts = 3;

    private static readonly TimeSpan FailedRetryAfter = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultMirrorCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxThrottleWait = TimeSpan.FromSeconds(10);
    private static readonly ConcurrentDictionary<string, Slot> Slots = new();
    private static readonly SemaphoreSlim Gate = new(MaxConcurrentRequests);
    private static CancellationTokenSource lifetime = new();
    private static HttpClient? http;
    private static long mirrorCooldownUntilTicks;

    private static HttpClient Http => http ??= CreateClient();

    /// <summary>The Chinese (Shanda) client prefers the mirror; every other client uses universalis.app.</summary>
    private static bool PreferMirror => Plugin.ClientState.ClientLanguage == ClientLanguage.ChineseSimplified;

    internal static RemoteSnapshot<T> GetOrRequest<T>(string path, TimeSpan ttl, Func<JsonElement, T?> parse) where T : class
    {
        var slot = Slots.GetOrAdd(path, _ => new Slot());
        lock (slot)
        {
            var stale = slot.Status switch
            {
                RemoteSnapshotStatus.Idle => true,
                RemoteSnapshotStatus.Ready => DateTimeOffset.Now - slot.FetchedAt > ttl,
                RemoteSnapshotStatus.Failed => DateTimeOffset.Now - slot.FetchedAt > FailedRetryAfter,
                _ => false,
            };
            if (stale)
            {
                slot.Status = slot.Value == null ? RemoteSnapshotStatus.Loading : RemoteSnapshotStatus.Refreshing;
                _ = FetchAsync(path, slot, parse);
            }

            return Snapshot<T>(slot);
        }
    }

    internal static IDisposable Observe<T>(string path, TimeSpan ttl, Func<JsonElement, T?> parse, Action<RemoteSnapshot<T>> callback) where T : class
    {
        var snapshot = GetOrRequest(path, ttl, parse);
        var slot = Slots[path];
        Action<Slot> handler = s => callback(Snapshot<T>(s));
        lock (slot)
            slot.Changed += handler;

        if (snapshot.HasValue)
            Plugin.Framework.RunOnFrameworkThread(() => callback(snapshot));

        return new Subscription(() =>
        {
            lock (slot)
                slot.Changed -= handler;
        });
    }

    internal static void Shutdown()
    {
        lifetime.Cancel();
        http?.Dispose();
        http = null;
        Slots.Clear();
        lifetime = new CancellationTokenSource();
    }

    private static RemoteSnapshot<T> Snapshot<T>(Slot slot) where T : class => new(slot.Status, slot.Value as T, slot.Error);

    private static async Task FetchAsync<T>(string path, Slot slot, Func<JsonElement, T?> parse) where T : class
    {
        try
        {
            var value = await DownloadAsync(path, parse, lifetime.Token).ConfigureAwait(false);
            lock (slot)
            {
                slot.Value = value;
                slot.Error = null;
                slot.Status = RemoteSnapshotStatus.Ready;
                slot.FetchedAt = DateTimeOffset.Now;
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            lock (slot)
            {
                slot.Error = ex.Message;
                slot.Status = RemoteSnapshotStatus.Failed;
                slot.FetchedAt = DateTimeOffset.Now;
            }

            Plugin.Log.Debug(ex, $"Universalis request failed: {path}");
        }

        // Subscribers mutate module state that the UI reads; hand them over on the framework thread.
        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            Action<Slot>? handlers;
            lock (slot)
                handlers = slot.Changed;
            handlers?.Invoke(slot);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches <paramref name="path"/> with at most <see cref="MaxConcurrentRequests"/> requests in flight. Rate-limit
    /// answers (429/503) from the mirror put it on cooldown for its Retry-After and the request goes to universalis.app;
    /// rate limits from universalis.app are retried after a short wait. Other HTTP errors fail immediately.
    /// </summary>
    private static async Task<T?> DownloadAsync<T>(string path, Func<JsonElement, T?> parse, CancellationToken token) where T : class
    {
        for (var attempt = 1; ; attempt++)
        {
            TimeSpan wait;
            await Gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using var response = await SendAsync(path, token).ConfigureAwait(false);
                if (!IsRateLimited(response.StatusCode) || attempt >= MaxAttempts)
                {
                    response.EnsureSuccessStatusCode();
                    await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
                    return parse(document.RootElement);
                }

                wait = Min(RetryAfter(response) ?? TimeSpan.FromSeconds(attempt * 2), MaxThrottleWait);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == null && attempt < MaxAttempts)
            {
                // Network-level failure (timeout, reset): back off briefly and try again.
                wait = TimeSpan.FromSeconds(attempt * 2);
            }
            finally
            {
                Gate.Release();
            }

            await Task.Delay(wait, token).ConfigureAwait(false);
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(string path, CancellationToken token)
    {
        if (PreferMirror && DateTime.UtcNow.Ticks >= Interlocked.Read(ref mirrorCooldownUntilTicks))
        {
            var mirrored = await Http.GetAsync(ChinaMirrorBaseUrl + path, token).ConfigureAwait(false);
            if (!IsRateLimited(mirrored.StatusCode))
                return mirrored;

            var cooldown = RetryAfter(mirrored) ?? DefaultMirrorCooldown;
            Interlocked.Exchange(ref mirrorCooldownUntilTicks, (DateTime.UtcNow + cooldown).Ticks);
            Plugin.Log.Debug($"Universalis mirror rate limited ({(int)mirrored.StatusCode}); using universalis.app for {cooldown.TotalSeconds:0}s");
            mirrored.Dispose();
        }

        return await Http.GetAsync(OfficialBaseUrl + path, token).ConfigureAwait(false);
    }

    private static bool IsRateLimited(System.Net.HttpStatusCode status) =>
        status is System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable;

    private static TimeSpan? RetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - DateTimeOffset.UtcNow,
            _ => null,
        };

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Pipi", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    internal static string Path(string endpoint, string scope, IEnumerable<uint> itemIds, string? query = null)
    {
        var ids = string.Join(',', itemIds);
        var path = endpoint.Length == 0
            ? $"{Uri.EscapeDataString(scope)}/{ids}"
            : $"{endpoint}/{Uri.EscapeDataString(scope)}/{ids}";
        return string.IsNullOrEmpty(query) ? path : $"{path}?{query}";
    }

    /// <summary>Single-item responses are the item object itself; multi-item ones wrap items in <c>items</c>.</summary>
    internal static Dictionary<uint, TItem> ParseItems<TItem>(JsonElement root) where TItem : class
    {
        var result = new Dictionary<uint, TItem>();
        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in items.EnumerateObject())
            {
                if (uint.TryParse(property.Name, out var id) && property.Value.Deserialize<TItem>(JsonOptions) is { } item)
                    result[id] = item;
            }
        }
        else if (root.TryGetProperty("itemID", out var idElement) && idElement.TryGetUInt32(out var id) &&
                 root.Deserialize<TItem>(JsonOptions) is { } item)
        {
            result[id] = item;
        }

        return result;
    }

    internal static uint WorldIdOf(JsonElement root) =>
        root.TryGetProperty("worldID", out var world) && world.TryGetUInt32(out var id) ? id : 0;

    private sealed class Slot
    {
        public RemoteSnapshotStatus Status = RemoteSnapshotStatus.Idle;
        public object? Value;
        public string? Error;
        public DateTimeOffset FetchedAt;
        public Action<Slot>? Changed;
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref dispose, null)?.Invoke();
    }
}

public static class RemoteUniversalisCatalog
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

    public static RemoteSnapshot<List<UniversalisDataCenter>> GetDataCentersOrRequest() =>
        Universalis.GetOrRequest("data-centers", Ttl, root => root.Deserialize<List<UniversalisDataCenter>>(Universalis.JsonOptions));

    public static RemoteSnapshot<List<UniversalisWorld>> GetWorldsOrRequest() =>
        Universalis.GetOrRequest("worlds", Ttl, root => root.Deserialize<List<UniversalisWorld>>(Universalis.JsonOptions));

    public static bool TryGetDataCenters(out List<UniversalisDataCenter> dataCenters)
    {
        dataCenters = GetDataCentersOrRequest().Value ?? [];
        return dataCenters.Count > 0;
    }

    public static bool TryGetWorlds(out List<UniversalisWorld> worlds)
    {
        worlds = GetWorldsOrRequest().Value ?? [];
        return worlds.Count > 0;
    }
}

public static class RemoteUniversalisMarket
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);

    public static RemoteSnapshot<UniversalisMarketDataResponse> GetOrRequest(
        uint[] itemIds, string scope, UniversalisMarketDataRequestParams? parameters = null) =>
        Universalis.GetOrRequest(Path(itemIds, scope, parameters), Ttl, Parse);

    public static IDisposable Observe(
        uint[] itemIds, string scope, Action<RemoteSnapshot<UniversalisMarketDataResponse>> callback, UniversalisMarketDataRequestParams? parameters = null) =>
        Universalis.Observe(Path(itemIds, scope, parameters), Ttl, Parse, callback);

    private static string Path(uint[] itemIds, string scope, UniversalisMarketDataRequestParams? parameters) =>
        Universalis.Path(string.Empty, scope, itemIds, parameters?.Query());

    private static UniversalisMarketDataResponse Parse(JsonElement root) => new()
    {
        WorldID = Universalis.WorldIdOf(root),
        Items = Universalis.ParseItems<UniversalisMarketItemData>(root),
    };
}

public static class RemoteUniversalisHistory
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public static RemoteSnapshot<UniversalisMarketHistoryResponse> GetOrRequest(
        uint[] itemIds, string scope, UniversalisMarketHistoryRequestParams? parameters = null) =>
        Universalis.GetOrRequest(Path(itemIds, scope, parameters), Ttl, Parse);

    public static IDisposable Observe(
        uint[] itemIds, string scope, Action<RemoteSnapshot<UniversalisMarketHistoryResponse>> callback, UniversalisMarketHistoryRequestParams? parameters = null) =>
        Universalis.Observe(Path(itemIds, scope, parameters), Ttl, Parse, callback);

    private static string Path(uint[] itemIds, string scope, UniversalisMarketHistoryRequestParams? parameters) =>
        Universalis.Path("history", scope, itemIds, (parameters ?? new UniversalisMarketHistoryRequestParams()).Query());

    private static UniversalisMarketHistoryResponse Parse(JsonElement root) => new()
    {
        WorldID = Universalis.WorldIdOf(root),
        Items = Universalis.ParseItems<UniversalisMarketHistoryItem>(root),
    };
}

public static class RemoteUniversalisAggregatedMarket
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);

    public static RemoteSnapshot<UniversalisAggregatedMarketDataResponse> GetOrRequest(uint[] itemIds, string scope) =>
        Universalis.GetOrRequest(Universalis.Path("aggregated", scope, itemIds), Ttl, Parse);

    public static IDisposable Observe(uint[] itemIds, string scope, Action<RemoteSnapshot<UniversalisAggregatedMarketDataResponse>> callback) =>
        Universalis.Observe(Universalis.Path("aggregated", scope, itemIds), Ttl, Parse, callback);

    private static UniversalisAggregatedMarketDataResponse? Parse(JsonElement root)
    {
        var response = root.Deserialize<UniversalisAggregatedMarketDataResponse>(Universalis.JsonOptions);
        response?.Normalize();
        return response;
    }
}
