using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.CMU14.Round;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Content.Server.CMU14.HelpRelay;

/// <summary>
/// Single Discord delivery pipe for the help systems (ahelp/mhelp/bughelp): one webhook endpoint
/// with per-source overrides, one queue, one HttpClient, one embed family. Conversations and reports
/// stay their own systems; only what happens after a player types a help command lives here.
/// </summary>
public sealed class HelpRelayManager : IPostInjectInit
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IServerDbManager _db = default!;

    private readonly HttpClient _http = new();
    private readonly ConcurrentQueue<(string Url, string PayloadJson, string? AttachmentJson)> _pending = new();
    private readonly ConcurrentDictionary<NetUserId, ulong?> _discordIds = new();
    private int _queueRunning;
    private ISawmill _sawmill = default!;

    private string _webhook = string.Empty;
    private string _webhookAHelp = string.Empty;
    private string _webhookMHelp = string.Empty;
    private string _webhookBugHelp = string.Empty;
    private string _revision = string.Empty;
    private string _serverName = string.Empty;

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("helprelay");
        _config.OnValueChanged(CCVars.HelpRelayWebhook, v => _webhook = v, true);
        _config.OnValueChanged(CCVars.HelpRelayWebhookAHelp, v => _webhookAHelp = v, true);
        _config.OnValueChanged(CCVars.HelpRelayWebhookMHelp, v => _webhookMHelp = v, true);
        _config.OnValueChanged(CCVars.HelpRelayWebhookBugHelp, v => _webhookBugHelp = v, true);
        _config.OnValueChanged(CCVars.HelpRelayRevision, v => _revision = v, true);
        _config.OnValueChanged(CCVars.AdminLogsServerName, v => _serverName = v, true);
    }

    public bool IsEnabled(HelpSource source)
    {
        return !string.IsNullOrWhiteSpace(UrlFor(source));
    }

    /// <summary>
    /// Queues one message for Discord delivery and returns; the spaced background task posts it.
    /// Call with <see cref="HelpPost.Context"/> set (or from the main thread) — context gathering
    /// touches entity systems, which background threads must not.
    /// </summary>
    public void Post(HelpSource source, HelpPost post)
    {
        var url = UrlFor(source);
        if (string.IsNullOrWhiteSpace(url))
            return;

        var context = post.Context ?? GetContext();
        var payload = HelpRelayPayload.Build(source, post with { Context = context }, NullIfEmpty(_revision));
        _pending.Enqueue((url, JsonSerializer.Serialize(payload), post.AttachmentJson));
        StartQueueProcessor();
    }

    /// <summary>
    /// Verified ckey → Discord id from rmc_linked_accounts, cached per player for the session.
    /// Null when unlinked.
    /// </summary>
    public Task<ulong?> GetLinkedDiscordId(NetUserId userId)
    {
        if (_discordIds.TryGetValue(userId, out var cached))
            return Task.FromResult(cached);

        return LoadAndCache();

        async Task<ulong?> LoadAndCache()
        {
            var id = await _db.GetLinkedDiscordId(userId.UserId, CancellationToken.None);
            _discordIds[userId] = id;
            return id;
        }
    }

    /// <summary>
    /// Round context for embeds and the bug report artifact. Main thread only (entity systems).
    /// </summary>
    public HelpContext GetContext()
    {
        var ticker = _entMan.System<GameTicker>();
        var auRound = _entMan.System<AuRoundSystem>();
        return new HelpContext
        {
            RoundId = ticker.RoundId,
            Elapsed = ticker.RoundDuration(),
            Gamemode = ticker.CurrentPreset?.ID ?? ticker.Preset?.ID ?? auRound.SelectedPreset?.ID,
            Map = auRound.GetSelectedPlanetId(),
            Server = _serverName,
        };
    }

    private string? UrlFor(HelpSource source) => source switch
    {
        HelpSource.AHelp => NullIfEmpty(_webhookAHelp) ?? NullIfEmpty(_webhook),
        HelpSource.MHelp => NullIfEmpty(_webhookMHelp) ?? NullIfEmpty(_webhook),
        _ => NullIfEmpty(_webhookBugHelp) ?? NullIfEmpty(_webhook),
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // Posts are spaced out because Discord rate limits per webhook harder than a help burst.
    private void StartQueueProcessor()
    {
        if (Interlocked.Exchange(ref _queueRunning, 1) == 1)
            return;

        Task.Run(ProcessQueueAsync);
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            while (_pending.TryDequeue(out var item))
            {
                await PostWebhook(item);
                await Task.Delay(1200);
            }

            _queueRunning = 0;

            if (_pending.IsEmpty || Interlocked.Exchange(ref _queueRunning, 1) == 1)
                return;
        }
    }

    private async Task PostWebhook((string Url, string PayloadJson, string? AttachmentJson) item)
    {
        try
        {
            using HttpContent content = item.AttachmentJson is { } attachment
                ? Multipart(item.PayloadJson, attachment)
                : new StringContent(item.PayloadJson, Encoding.UTF8, "application/json");

            using var response = await _http.PostAsync(item.Url, content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _sawmill.Error($"Help relay webhook failed: {(int) response.StatusCode} {response.StatusCode}\n{body}");
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Help relay webhook error: {e}");
        }
    }

    private static MultipartFormDataContent Multipart(string payloadJson, string attachmentJson)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");
        form.Add(new StringContent(attachmentJson, Encoding.UTF8, "application/json"), "files[0]", "report.json");
        return form;
    }
}
