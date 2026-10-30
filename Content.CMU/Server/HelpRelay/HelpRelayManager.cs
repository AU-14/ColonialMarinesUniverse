using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.CMU14.Round;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Utility;

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
    [Dependency] private readonly IResourceManager _res = default!;

    // Failed one-shot posts retry briefly then spool to disk, and the backlog dies with the round:
    // a dead webhook must not accumulate a queue over days of uptime. Posts are spaced out
    // because Discord rate limits per webhook harder than a help burst.
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PostSpacing = TimeSpan.FromMilliseconds(1200);
    private const int MaxQueued = 64;

    // Dropped posts land here for manual recovery; see SpoolDropped.
    private static readonly ResPath DroppedPostDir = new("/dropped_relay");

    private readonly HttpClient _http = new();
    private readonly ConcurrentQueue<(HelpSource Source, string Url, string PayloadJson, string? AttachmentJson, int Attempts)> _pending = new();
    private readonly ConcurrentDictionary<NetUserId, ulong?> _discordIds = new();
    private int _queueRunning;
    private int _spoolSeq;
    // Bumped on round-end drain: an item held by the processor across a drain is stale, and
    // re-enqueueing it would carry the backlog past the round the drain exists to end.
    private int _roundGen;
    private ISawmill _sawmill = default!;

    // One Discord message per conversation, edited in place as lines arrive (the ahelp relay model).
    // Main-thread only: created and cleared there; only the owning processor task mutates the rest.
    private readonly Dictionary<NetUserId, HelpConversation> _conversations = new();

    private sealed class HelpConversation
    {
        public string? Url;
        public HelpSource Source;
        public string Username = string.Empty;
        public string? Revision;
        public string? BuiltAt;
        public string? Server;
        public string? MessageId;
        public string? ChannelId;
        public string? GuildId;
        public string Description = string.Empty;
        public GameRunLevel LastRunLevel;
        public readonly ConcurrentQueue<string> Pending = new();
        public int Processing;
    }

    private string _webhook = string.Empty;
    private string _webhookAHelp = string.Empty;
    private string _webhookMHelp = string.Empty;
    private string _webhookBugHelp = string.Empty;
    private string _revision = string.Empty;
    private string _builtAt = string.Empty;
    private string _serverName = string.Empty;

    void IPostInjectInit.PostInject()
    {
        _sawmill = _log.GetSawmill("helprelay");
    }

    /// <summary>
    /// CVar subscriptions live here, not in PostInject: unit tests build the IoC graph before
    /// content CVars are registered, and OnValueChanged would throw for every fixture.
    /// </summary>
    public void Initialize()
    {
        _config.OnValueChanged(CCVars.HelpRelayWebhook, v => _webhook = v, true);
        _config.OnValueChanged(CCVars.HelpRelayWebhookAHelp, v => _webhookAHelp = v, true);
        _config.OnValueChanged(CCVars.HelpRelayWebhookMHelp, v => _webhookMHelp = v, true);
        _config.OnValueChanged(CCVars.HelpRelayWebhookBugHelp, v => _webhookBugHelp = v, true);
        _config.OnValueChanged(CCVars.HelpRelayRevision, v => _revision = v, true);
        _config.OnValueChanged(CCVars.HelpRelayBuiltAt, v => _builtAt = v, true);
        _config.OnValueChanged(CCVars.AdminLogsServerName, v => _serverName = v, true);
    }

    public bool IsEnabled(HelpSource source)
        => !string.IsNullOrWhiteSpace(UrlFor(source));

    /// <summary>
    /// Queues one message for Discord delivery and returns; the spaced background task posts it.
    /// Delivery is best-effort: failed posts retry briefly then spool to disk, and a full queue spools new posts.
    /// Call with <see cref="HelpPost.Context"/> set (or from the main thread) — context gathering
    /// touches entity systems, which background threads must not.
    /// </summary>
    public void Post(HelpSource source, HelpPost post)
    {
        var url = UrlFor(source);
        if (string.IsNullOrWhiteSpace(url))
            return;

        var context = post.Context ?? GetContext();
        var payloadJson = JsonSerializer.Serialize(HelpRelayPayload.Build(source, post with { Context = context }, NullIfEmpty(_revision), NullIfEmpty(_builtAt)));
        if (_pending.Count >= MaxQueued)
        {
            _sawmill.Error($"Help relay queue full ({MaxQueued} posts), dropping a new one");
            SpoolDropped(source, payloadJson, post.AttachmentJson);
            return;
        }

        _pending.Enqueue((source, url, payloadJson, post.AttachmentJson, 0));
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

    /// <summary>
    /// Queues one line into the conversation's Discord message: created on first line, then edited in
    /// place per line (the ahelp relay model). Main thread only — reads round state from entity systems.
    /// </summary>
    public void RelayConversationLine(
        HelpSource source,
        NetUserId conversationId,
        string username,
        bool fromStaff,
        string speaker,
        string text)
    {
        var url = UrlFor(source);
        if (string.IsNullOrWhiteSpace(url))
            return;

        var conversation = _conversations.GetOrNew(conversationId);
        if (conversation.Url == null)
        {
            conversation.Source = source;
            conversation.Url = url;
            conversation.Username = username;
            conversation.Revision = NullIfEmpty(_revision);
            conversation.BuiltAt = NullIfEmpty(_builtAt);
            conversation.Server = NullIfEmpty(_serverName);
            conversation.LastRunLevel = CurrentRunLevel();
        }

        var runLevel = CurrentRunLevel();
        if (runLevel != conversation.LastRunLevel)
        {
            conversation.Pending.Enqueue(HelpRelayPayload.BuildRunLevelSeparator(runLevel));
            conversation.LastRunLevel = runLevel;
        }

        var roundTime = runLevel == GameRunLevel.InRound
            ? _entMan.System<GameTicker>().RoundDuration().ToString(@"hh\:mm\:ss")
            : null;
        conversation.Pending.Enqueue(HelpRelayPayload.BuildConversationLine(speaker, fromStaff, roundTime, text));
        ProcessConversation(conversation);
    }

    /// <summary>
    /// Round restart clears per-round relay state: conversations end (the next line starts a fresh
    /// message), linked-Discord lookups re-resolve next round, and the one-shot post queue dies with
    /// the round so a failing webhook cannot carry a backlog across rounds.
    /// </summary>
    public void ClearRoundState()
    {
        _conversations.Clear();
        _discordIds.Clear();
        Interlocked.Increment(ref _roundGen);

        var dropped = 0;
        while (_pending.TryDequeue(out var item))
        {
            SpoolDropped(item.Source, item.PayloadJson, item.AttachmentJson);
            dropped++;
        }

        if (dropped > 0)
            _sawmill.Warning($"Help relay cleared {dropped} undelivered post(s) at round end");
    }

    private GameRunLevel CurrentRunLevel() => _entMan.System<GameTicker>().RunLevel;

    private void ProcessConversation(HelpConversation conversation)
    {
        if (Interlocked.Exchange(ref conversation.Processing, 1) == 1)
            return;

        Task.Run(async () =>
        {
            while (true)
            {
                while (conversation.Pending.TryDequeue(out var line))
                    await AppendConversationLine(conversation, line);

                conversation.Processing = 0;

                if (conversation.Pending.IsEmpty || Interlocked.Exchange(ref conversation.Processing, 1) == 1)
                    return;
            }
        });
    }

    private async Task AppendConversationLine(HelpConversation conversation, string line)
    {
        if (conversation.Description.Length + line.Length + 1 > HelpRelayPayload.ConversationDescriptionMax)
        {
            // Link back to the previous transcript message when the create response carried ids.
            var link = conversation.GuildId is { } guild
                && conversation.ChannelId is { } channel
                && conversation.MessageId is { } message
                ? $"_Continued from [this message](https://discord.com/channels/{guild}/{channel}/{message})._\n"
                : "_(transcript continued)_\n";
            conversation.Description = link + line;
            conversation.MessageId = null;
        }
        else
        {
            conversation.Description += $"\n{line}";
        }

        var payload = HelpRelayPayload.BuildConversationPayload(
            conversation.Source,
            conversation.Username,
            conversation.Description,
            conversation.Revision,
            conversation.Server,
            conversation.BuiltAt);
        var json = JsonSerializer.Serialize(payload);

        try
        {
            if (conversation.MessageId == null)
            {
                using var response = await _http.PostAsync(
                    $"{conversation.Url}?wait=true",
                    new StringContent(json, Encoding.UTF8, "application/json"));
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _sawmill.Error($"Help relay conversation post failed: {(int) response.StatusCode} {response.StatusCode}\n{content}");
                    return;
                }

                var node = JsonNode.Parse(content);
                var id = node?["id"]?.ToString();
                if (id == null)
                {
                    _sawmill.Error($"Help relay could not find message id in webhook response: {content}");
                    return;
                }

                conversation.MessageId = id;
                conversation.ChannelId = node?["channel_id"]?.ToString();
                conversation.GuildId = node?["guild_id"]?.ToString();
            }
            else
            {
                using var response = await _http.PatchAsync(
                    $"{conversation.Url}/messages/{conversation.MessageId}",
                    new StringContent(json, Encoding.UTF8, "application/json"));
                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _sawmill.Error($"Help relay conversation edit failed: {(int) response.StatusCode} {response.StatusCode}\n{content}");
                    // The message may have been deleted; fall back to posting a fresh one next line.
                    conversation.MessageId = null;
                }
            }
        }
        catch (Exception e) { _sawmill.Error($"Help relay conversation error: {e}"); }
    }

    private string? UrlFor(HelpSource source) => source switch
    {
        HelpSource.AHelp => NullIfEmpty(_webhookAHelp) ?? NullIfEmpty(_webhook),
        HelpSource.MHelp => NullIfEmpty(_webhookMHelp) ?? NullIfEmpty(_webhook),
        _ => NullIfEmpty(_webhookBugHelp) ?? NullIfEmpty(_webhook),
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

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
                var gen = Volatile.Read(ref _roundGen);
                var sent = await PostWebhook((item.Url, item.PayloadJson, item.AttachmentJson, item.Attempts));

                if (!sent && gen == Volatile.Read(ref _roundGen) && item.Attempts < MaxRetries)
                {
                    _sawmill.Warning($"Help relay post failed, retry {item.Attempts + 1}/{MaxRetries}");
                    await Task.Delay(RetryDelay);
                    _pending.Enqueue((item.Source, item.Url, item.PayloadJson, item.AttachmentJson, item.Attempts + 1));
                    continue;
                }

                if (!sent)
                {
                    _sawmill.Error("Help relay post dropped after retries, webhook unreachable");
                    SpoolDropped(item.Source, item.PayloadJson, item.AttachmentJson);
                }

                await Task.Delay(PostSpacing);
            }

            _queueRunning = 0;

            if (_pending.IsEmpty || Interlocked.Exchange(ref _queueRunning, 1) == 1)
                return;
        }
    }

    private async Task<bool> PostWebhook((string Url, string PayloadJson, string? AttachmentJson, int Attempts) item)
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

            return response.IsSuccessStatusCode;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Help relay webhook error: {e}");
            return false;
        }
    }

    /// <summary>
    /// Last-ditch persistence for a post the relay gave up on: the reporter was already told it was
    /// sent, so the payload lands on disk for manual recovery instead of vanishing with the round.
    /// The webhook URL never goes in the file.
    /// </summary>
    private void SpoolDropped(HelpSource source, string payloadJson, string? attachmentJson)
    {
        try
        {
            _res.UserData.CreateDir(DroppedPostDir);
            var seq = Interlocked.Increment(ref _spoolSeq);
            var path = DroppedPostDir / $"{source.ToString().ToLowerInvariant()}_{DateTime.UtcNow:yyyyMMdd-HHmmssfff}_{seq:D3}.json";
            var dropped = new DroppedPost
            {
                Source = source.ToString(),
                DroppedAt = DateTime.UtcNow.ToString("o"),
                Payload = JsonNode.Parse(payloadJson),
                Attachment = attachmentJson is null ? null : JsonNode.Parse(attachmentJson),
            };
            _res.UserData.WriteAllText(path, JsonSerializer.Serialize(dropped));
            _sawmill.Warning($"Spooled a dropped help relay post to {path}");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to spool a dropped help relay post:\n{e}");
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
