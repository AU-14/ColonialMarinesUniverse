using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Server.CMU14.HelpRelay;
using Content.Server.Chat.Managers;
using Content.Server.Players.RateLimiting;
using Content.Shared.CCVar;
using Content.Shared.CMU14.BugHelp;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Players.RateLimiting;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.BugHelp;

/// <summary>
/// Turns a player's bughelp message into a report and hands it to the help relay,
/// which posts it to the bug report Discord forum channel as one thread per report.
/// </summary>
public sealed class BugHelpSystem : EntitySystem
{
    private const string RateLimitKey = "BugHelp";
    private const float NearbyRadius = 3f;
    private const int NearbyCap = 5;
    private const int TitleCap = 100;
    private const int WhatHappenedCap = 2000;
    private const int OptionalTextCap = 1000;
    private const int ExamineCap = 2000;
    private const int ItemQueryCap = 64;
    private const int ItemCandidatesMax = 5;

    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly HelpRelayManager _relay = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly PlayerRateLimitManager _rateLimit = default!;

    private int _sequence;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _rateLimit.Register(
            RateLimitKey,
            new RateLimitRegistration(
                CCVars.BugHelpRateLimitPeriod,
                CCVars.BugHelpRateLimitCount,
                _ => { })
        );
        _sawmill = _log.GetSawmill("bughelp");
        _net.RegisterNetMessage<BugHelpOpenMsg>();
        _net.RegisterNetMessage<BugHelpSubmitMsg>(OnBugHelpSubmit);
    }

    private void OnBugHelpSubmit(BugHelpSubmitMsg message)
    {
        if (!_playerManager.TryGetSessionById(message.MsgChannel.UserId, out var session))
            return;

        var outcome = TrySubmit(
            session,
            message.WhatHappened,
            message.Expected,
            message.Steps,
            message.ItemQuery,
            message.ExamineText,
            out var reportId);
        _chat.DispatchServerMessage(session, AckString(outcome, reportId));
    }

    /// <summary>The same Fluent acks the command uses, delivered to window users in chat.</summary>
    public static string AckString(BugHelpOutcome outcome, string? reportId) => outcome switch
    {
        BugHelpOutcome.Sent => Loc.GetString("cmu-cmd-bughelp-sent", ("id", reportId!)),
        BugHelpOutcome.Disabled => Loc.GetString("cmu-cmd-bughelp-disabled"),
        BugHelpOutcome.RateLimited => Loc.GetString("cmu-cmd-bughelp-rate-limited"),
        BugHelpOutcome.NoEntity => Loc.GetString("cmu-cmd-bughelp-no-entity"),
        _ => Loc.GetString("cmu-bughelp-window-empty"),
    };

    private void OnRoundRestart(RoundRestartCleanupEvent args)
        => _sequence = 0;

    private string? PrototypeId(EntityUid uid) => MetaData(uid).EntityPrototype?.ID;
    private string? OrNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string Cap(string value, int cap) => value.Length > cap ? value[..cap] : value;
    private static string? Optional(string? value, int cap) =>
        string.IsNullOrWhiteSpace(value) ? null : Cap(value.Trim(), cap);

    /// <summary>
    /// Builds the report and hands it to the help relay. All entity state is read synchronously
    /// here; only the Discord link lookup and the HTTP post run off-thread.
    /// </summary>
    public BugHelpOutcome TrySubmit(
        ICommonSession player,
        string whatHappened,
        string? expected,
        string? steps,
        string? itemQuery,
        string? examineText,
        out string? reportId)
    {
        reportId = null;
        whatHappened = Cap(whatHappened.Trim(), WhatHappenedCap);
        expected = Optional(expected, OptionalTextCap);
        steps = Optional(steps, OptionalTextCap);
        examineText = Optional(examineText, ExamineCap);
        itemQuery = Optional(itemQuery, ItemQueryCap);

        if (string.IsNullOrWhiteSpace(whatHappened))
            return BugHelpOutcome.Empty;

        if (!_relay.IsEnabled(HelpSource.BugHelp))
            return BugHelpOutcome.Disabled;

        if (_rateLimit.CountAction(player, RateLimitKey) == RateLimitStatus.Blocked)
            return BugHelpOutcome.RateLimited;

        if (player.AttachedEntity is not { } attached || EntityManager.Deleted(attached))
            return BugHelpOutcome.NoEntity;

        var context = _relay.GetContext();
        var report = new BugReport
        {
            Id = $"BUG-R{context.RoundId}-{++_sequence:D2}",
            Server = context.Server,
            Revision = OrNull(_config.GetCVar(CCVars.HelpRelayRevision)),
            ReportedAt = DateTime.UtcNow.ToString("o"),
            Round = new BugRound
            {
                Id = context.RoundId,
                Elapsed = context.Elapsed.ToString(@"hh\:mm\:ss"),
                Gamemode = context.Gamemode,
                Map = context.Map,
            },
            Reporter = new BugReporter
            {
                Ckey = player.Name,
                Character = MetaData(attached).EntityName,
                Job = GetJobId(attached),
            },
            Report = new BugReportBody
            {
                WhatHappened = whatHappened,
                Expected = expected,
                Steps = steps,
                ItemQuery = itemQuery,
                ItemCandidates = itemQuery == null ? null : ResolveItemCandidates(itemQuery),
                ExamineText = examineText,
            },
            Snapshot = GatherSnapshot(attached),
        };

        var userId = player.UserId;
        Task.Run(async () => await PostReportAsync(report, userId, context));

        reportId = report.Id;
        return BugHelpOutcome.Sent;
    }

    private string? GetJobId(EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _)
            || !_jobs.MindTryGetJobId(mindId, out var job)
            || job is not { } jobId)
            return null;

        return jobId;
    }

    private BugSnapshot GatherSnapshot(EntityUid player)
    {
        var xform = Transform(player);

        string? activeHand = null;
        if (_hands.GetActiveItem(player) is { } held)
            activeHand = PrototypeId(held);

        var clothing = new Dictionary<string, string?>();
        if (_inventory.TryGetContainerSlotEnumerator(player, out var slots))
        {
            while (slots.NextItem(out var item, out var slot))
                clothing[slot.Name] = PrototypeId(item);
        }

        var nearby = new List<string>();
        foreach (var ent in _lookup.GetEntitiesInRange(xform.Coordinates, NearbyRadius))
        {
            if (ent == player)
                continue;

            var protoId = PrototypeId(ent);
            if (protoId == null || nearby.Contains(protoId))
                continue;

            nearby.Add(protoId);
            if (nearby.Count >= NearbyCap)
                break;
        }

        return new BugSnapshot
        {
            ActiveHand = activeHand,
            Clothing = clothing,
            Position = new BugPosition
            {
                Grid = xform.GridUid is { } grid ? MetaData(grid).EntityName : null,
                X = (int) xform.Coordinates.X,
                Y = (int) xform.Coordinates.Y,
            },
            Nearby = nearby,
        };
    }

    private List<BugItemCandidate> ResolveItemCandidates(string query)
    {
        var normalized = query.ToLowerInvariant();
        var candidates = new List<(string Id, double Score)>();

        foreach (var prototype in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.Abstract)
                continue;

            var name = prototype.Name;
            if (Loc.TryGetString(name, out var localized))
                name = localized;

            var score = ScoreItem(normalized, prototype.ID.ToLowerInvariant(), name.ToLowerInvariant());
            if (score > 0)
                candidates.Add((prototype.ID, score));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Id.Length)
            .Take(ItemCandidatesMax)
            .Select(candidate => new BugItemCandidate { Id = candidate.Id, Score = candidate.Score })
            .ToList();
    }

    /// <summary>Substring scorer over already-normalized surfaces. Exposed for tests.</summary>
    internal static double ScoreItem(string query, string id, string name)
    {
        if (id == query)
            return 1.0;

        if (name == query)
            return 0.95;

        if (id.Contains(query))
            return 0.6 + (id.StartsWith(query) ? 0.1 : 0);

        if (name.Contains(query))
            return 0.55 + (name.StartsWith(query) ? 0.1 : 0);

        if (Words(id).Any(word => word.StartsWith(query))
            || Words(name).Any(word => word.StartsWith(query)))
            return 0.3;

        return 0;

        static IEnumerable<string> Words(string value) => value.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task PostReportAsync(BugReport report, NetUserId userId, HelpContext context)
    {
        try
        {
            report.Reporter.DiscordId = await _relay.GetLinkedDiscordId(userId);

            var title = $"{report.Id} · {report.Report.WhatHappened}"
                .Replace('\n', ' ')
                .Replace('\r', ' ');
            if (title.Length > TitleCap)
                title = title[..TitleCap];

            var reporter = report.Reporter;
            var job = reporter.Job ?? "no job";
            _relay.Post(HelpSource.BugHelp, new HelpPost
            {
                Author = $"{reporter.Ckey} ({reporter.Character} · {job})",
                Title = title,
                Description = report.Report.WhatHappened,
                Context = context,
                AttachmentJson = JsonSerializer.Serialize(report),
            });
        }
        catch (Exception e) { _sawmill.Error($"BugHelp failed to prepare {report.Id}:\n{e}"); }
    }
}

public enum BugHelpOutcome
{
    Sent,
    Disabled,
    RateLimited,
    NoEntity,
    Empty,
}

sealed class BugReport
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = "cmu-bugreport/1";
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("server")] public string Server { get; set; } = string.Empty;
    [JsonPropertyName("revision")] public string? Revision { get; set; }
    [JsonPropertyName("reported_at")] public string ReportedAt { get; set; } = string.Empty;
    [JsonPropertyName("round")] public BugRound Round { get; set; } = new();
    [JsonPropertyName("reporter")] public BugReporter Reporter { get; set; } = new();
    [JsonPropertyName("report")] public BugReportBody Report { get; set; } = new();
    [JsonPropertyName("snapshot")] public BugSnapshot Snapshot { get; set; } = new();
}

sealed class BugRound
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("elapsed")] public string Elapsed { get; set; } = "00:00:00";
    [JsonPropertyName("gamemode")] public string? Gamemode { get; set; }
    [JsonPropertyName("map")] public string? Map { get; set; }
}

sealed class BugReporter
{
    [JsonPropertyName("ckey")] public string Ckey { get; set; } = string.Empty;
    [JsonPropertyName("character")] public string? Character { get; set; }
    [JsonPropertyName("job")] public string? Job { get; set; }
    [JsonPropertyName("discord_id")] public ulong? DiscordId { get; set; }
}

sealed class BugReportBody
{
    [JsonPropertyName("what_happened")] public string WhatHappened { get; set; } = string.Empty;

    [JsonPropertyName("expected")] public string? Expected { get; set; }
    [JsonPropertyName("steps")] public string? Steps { get; set; }
    [JsonPropertyName("item_query")] public string? ItemQuery { get; set; }
    [JsonPropertyName("item_candidates")] public List<BugItemCandidate>? ItemCandidates { get; set; }
    [JsonPropertyName("examine_text")] public string? ExamineText { get; set; }
}

sealed class BugItemCandidate
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("score")] public double Score { get; set; }
}

sealed class BugSnapshot
{
    [JsonPropertyName("active_hand")] public string? ActiveHand { get; set; }
    [JsonPropertyName("clothing")] public Dictionary<string, string?> Clothing { get; set; } = new();
    [JsonPropertyName("position")] public BugPosition Position { get; set; } = new();
    [JsonPropertyName("nearby")] public List<string> Nearby { get; set; } = new();
}

sealed class BugPosition
{
    [JsonPropertyName("grid")] public string? Grid { get; set; }
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
}
