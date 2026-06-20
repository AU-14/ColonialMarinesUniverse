// ┌──[ Colonial Marines Universe ]──────────────────────────────────┐
// │ · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · ·│
// │ Licensed under GNU Affero General Public License · · · · · · · ·
// │ · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · · ·│
// └─────────────────────────────────────────────────────────────────┘
//
//        This is intended to be run as a standalone executable.
//             dotnet run --project Content.MultiAuthProxy
//            Which can be integrated as a watchdog service.
//              sudo systemctl enable --now cmu-multiauth
//                sudo systemctl restart cmu-multiauth
//     We do not block, switch to or discourage one over the other.
//           We accept both parties and respect them equally.
//
// The new auth server, is actually a clone of the original, and overtime they will diverge.
// Old accounts and their unique IDs are shared on both, only new accounts receive new Ids.
// So the first checked auth will likely hit most results, it is the launchers which are problematic.
// The launchers conflict with another, which is why we need this proxy = the federation layer.
// On /api/session/hasJoined we fan out & *fastest* valid response wins and gets pinned to that user.
// routingCache -> overtime every known UID becomes a single request instead of a multiplexer to the
// providers: we only fallback on timeout, 404, invalid etc. & update/heal cache on success.
// The Admin queries (/api/query/name/ & /api/query/userid/) prefer the player-store.
//
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("authServers.json", optional: false);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
var app = builder.Build();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MultiAuthProxy");
var authServers = builder.Configuration.GetSection("AuthServers").Get<string[]>()!.Select(s => s.TrimEnd('/')).ToArray();
var timeoutMs = builder.Configuration.GetValue("TimeoutMs", 1500);
var cacheTtl = TimeSpan.FromDays(builder.Configuration.GetValue("CacheTtlDays", 30));
var storePath = builder.Configuration.GetValue("StorePath", "player-store.json")!;
var flushInterval = TimeSpan.FromSeconds(builder.Configuration.GetValue("FlushIntervalSeconds", 60));
var listen = builder.Configuration.GetValue("Listen", "http://localhost:9119");
const int HttpTimeoutMultiplier = 4;

// Must exceed the timeoutMs, so a cancel comes from CTS rather than the HttpClient timeout
var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs * HttpTimeoutMultiplier) };

var store = new PlayerStore(storePath, cacheTtl);
store.Load(logger);

using var flushTimer = new Timer(_ =>
{
    if (!store.IsDirty) return;
    try { store.Save(); logger.LogDebug("player-store flushed to disk"); }
    catch (Exception ex) { logger.LogError(ex, "player-store flush failed"); }
}, null, flushInterval, flushInterval);

app.Lifetime.ApplicationStopping.Register(() =>
{
    try { store.Save(); logger.LogInformation("player-store saved on shutdown ({Path})", storePath); }
    catch (Exception ex) { logger.LogError(ex, "player-store shutdown save failed"); }
});

app.MapGet("/api/session/hasJoined", async (string hash, string userId, string? serverUrl, CancellationToken ct) =>
{
    // Cached server first
    var cached = store.GetById(userId);
    if (cached != null)
    {
        try
        {
            var result = await CheckServer(cached.Server, ct);
            if (result?.RawBody != null)
            {
                logger.LogDebug("hasJoined userId={UserId} served from store auth={Server}", userId, cached.Server);
                UpdateStore(userId, result.Value.UserName, cached.Server);
                return Results.Bytes(result.Value.RawBody, "application/json");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "hasJoined cached auth={Server} failed for userId={UserId}; falling back to fan-out", cached.Server, userId);
        }

        store.Invalidate(userId);
        logger.LogInformation("hasJoined userId={UserId} cache invalid for auth={Server}; repairing via fan-out", userId, cached.Server);
    }


    // Multiplexer
    var winner = await FirstValid(authServers, CheckServer, ct, timeoutMs, logger);
    if (winner?.RawBody != null)
    {
        if (cached != null && cached.Server != winner.Value.Server)
            logger.LogWarning("hasJoined userId={UserId} server changed {Old} -> {New}; shared GUID or multi-server account?",
                userId, cached.Server, winner.Value.Server);

        logger.LogInformation("hasJoined userId={UserId} routedTo={Server} storeUpdated=true", userId, winner.Value.Server);
        UpdateStore(userId, winner.Value.UserName, winner.Value.Server);
        return Results.Bytes(winner.Value.RawBody, "application/json");
    }

    return Results.Ok(new { isValid = false, userData = (object?)null, connectionData = (object?)null });

    void UpdateStore(string uid, string? userName, string server)
    => store.Upsert(uid, userName, server);

    async Task<(byte[]? RawBody, string Server, string? UserName)?> CheckServer(string server, CancellationToken token)
    {
        var url = $"{server}/api/session/hasJoined?hash={hash}&userId={userId}";
        if (serverUrl != null)
            url += $"&serverUrl={Uri.EscapeDataString(serverUrl)}";

        using var resp = await http.GetAsync(url, token);
        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode == HttpStatusCode.NotFound)
                logger.LogDebug("hasJoined auth={Server} userId={UserId} - not found (expected)", server, userId);
            else
                logger.LogWarning("hasJoined auth={Server} userId={UserId} returned {StatusCode}!", server, userId, resp.StatusCode);
            return null;
        }

        var body = await resp.Content.ReadAsByteArrayAsync(token);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("isValid", out var isValid) && isValid.GetBoolean())
        {
            logger.LogDebug("hasJoined auth={Server} userId={UserId} valid=true", server, userId);
            var userName = doc.RootElement.TryGetProperty("userData", out var ud)
                && ud.TryGetProperty("userName", out var un)
                ? un.GetString() : null;
            return (body, server, userName);

        }

        logger.LogDebug("hasJoined auth={Server} userId={UserId} valid=false", server, userId);
        return null;
    }
});

app.MapGet("/api/query/name", async (string name, CancellationToken ct) =>
{
    var servers = Prefer(authServers, store.GetByName(name));
    return await ProxyQuery(servers, http, s => $"{s}/api/query/name?name={Uri.EscapeDataString(name)}", ct, logger, "query/name", name);
});

app.MapGet("/api/query/userid", async (string userid, CancellationToken ct) =>
{
    var servers = Prefer(authServers, store.GetById(userid));
    return await ProxyQuery(servers, http, s => $"{s}/api/query/userid?userid={Uri.EscapeDataString(userid)}", ct, logger, "query/userid", userid);
});

app.Run(listen);

static async Task<T?> FirstValid<T>(
    string[] servers,
    Func<string, CancellationToken, Task<T?>> query,
    CancellationToken outer,
    int timeoutMs,
    ILogger logger) where T : struct
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
    cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

    var tcs = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);
    var remaining = servers.Length;

    async Task Execute(string server)
    {
        try
        {
            var result = await query(server, cts.Token);
            if (result is not null && tcs.TrySetResult(result))
                cts.Cancel();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogWarning(ex, "fan-out auth={Server} failed", server); }
        finally
        {
            if (Interlocked.Decrement(ref remaining) == 0)
                tcs.TrySetResult(null);
        }
    }

    foreach (var server in servers)
        _ = Execute(server);

    var finalResult = await tcs.Task;
    cts.Cancel();
    return finalResult;
}

static string[] Prefer(string[] servers, PlayerRecord? record) =>
    record == null ? servers : new[] { record.Server }.Concat(servers.Where(s => s != record.Server)).ToArray();

static async Task<IResult> ProxyQuery(
    string[] servers, HttpClient http,
    Func<string, string> urlFactory,
    CancellationToken ct,
    ILogger logger,
    string endpointLabel,
    string lookupValue)
{
    foreach (var server in servers)
    {
        var url = urlFactory(server);

        try
        {
            using var resp = await http.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                logger.LogInformation("{Endpoint} lookup={LookupValue} servedBy={Server}", endpointLabel, lookupValue, server);
                return Results.Content(await resp.Content.ReadAsStringAsync(ct), "application/json");
            }

            logger.LogDebug("Query auth={Server} returned {StatusCode}", server, (int)resp.StatusCode);
        }
        catch (Exception ex) { logger.LogDebug(ex, "Query auth={Server} failed", server); }
    }

    logger.LogWarning("No auth server answered {Endpoint} for '{LookupValue}'. Tried: {Servers}", endpointLabel, lookupValue, string.Join(", ", servers));
    return Results.NotFound();
}
