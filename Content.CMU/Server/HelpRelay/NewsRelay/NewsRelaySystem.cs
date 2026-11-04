using System.Linq;
using System.Threading.Tasks;
using Content.Server.CMU14.HelpRelay.Systems;
using Content.Shared.GameTicking;
using Content.Shared.MassMedia.Components;
using Content.Shared.MassMedia.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.HelpRelay.NewsRelay;

/// <summary>
/// Delivers the round's station news to Discord at round end: one forum thread per round, one
/// message per article inside it, ordered by publish time. Upstream's own webhook path
/// (discord.news_webhook) stays dormant; this is the CMU replacement, riding the help relay.
/// Reporters show as their linked Discord account when one exists, their ckey otherwise.
/// </summary>
public sealed class NewsRelaySystem : EntitySystem
{
    [Dependency] private readonly HelpRelayManager _relay = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    // Main-thread only: filled at publish time, snapshotted at round end, cleared at restart.
    private readonly Dictionary<EntityUid, List<AuthoredArticle>> _authored = new();

    private readonly record struct AuthoredArticle(NewsArticle Article, string? Ckey, NetUserId? UserId);

    public override void Initialize()
    {
        _sawmill = _log.GetSawmill("newsrelay");
        SubscribeLocalEvent<StationNewsAuthoredEvent>(OnStationNewsAuthored);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEndMessage);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _authored.Clear());
    }

    private void OnStationNewsAuthored(ref StationNewsAuthoredEvent ev)
    {
        string? ckey = null;
        NetUserId? userId = null;
        if (ev.Actor is { } actor && TryComp<ActorComponent>(actor, out var session))
        {
            userId = session.PlayerSession.UserId;
            ckey = session.PlayerSession.Name;
        }

        _authored.GetOrNew(ev.Station).Add(new AuthoredArticle(ev.Article, ckey, userId));
    }

    private void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        if (!_relay.IsEnabled(HelpSource.News))
            return;

        // Round state dies with the round, so articles, authors and context are snapshotted on the
        // main thread before any delivery happens in the background.
        var reports = new List<AuthoredArticle>();
        var query = EntityQueryEnumerator<StationNewsComponent>();
        while (query.MoveNext(out var station, out var comp))
        {
            // Only articles still on the station are delivered, so mid-round deletions hold. Captures
            // pair by publish time; the writer cooldown spaces same-console articles, and a collision
            // can only swap two articles of one tick on the same station.
            var captures = _authored.GetValueOrDefault(station);
            var remaining = captures is { Count: > 0 } ? new List<AuthoredArticle>(captures) : null;
            foreach (var article in comp.Articles)
            {
                var report = new AuthoredArticle(article, null, null);
                if (remaining is { } left)
                {
                    var idx = left.FindIndex(c => c.Article.ShareTime == article.ShareTime);
                    if (idx >= 0)
                    {
                        report = left[idx];
                        left.RemoveAt(idx);
                    }
                }

                reports.Add(report);
            }
        }

        if (reports.Count == 0)
            return;

        reports.Sort(static (x, y) => x.Article.ShareTime.CompareTo(y.Article.ShareTime));
        var context = _relay.GetContext();
        var opener = new HelpPost
        {
            Title = "Station news",
            Description = $"**{reports.Count} article{(reports.Count == 1 ? "" : "s")} published this round.**\n"
                + string.Join("\n", reports.Select(r => $"• {r.Article.Title}")),
            Author = "round digest",
            ThreadName = $"Round {context.RoundId} · {context.Gamemode ?? "?"} · {context.Map ?? "?"}",
            Context = context,
        };

        Task.Run(async () =>
        {
            // Linked ids render the reporter's Discord name without pinging them: the payload stays
            // quiet, mentions never notify under it. Lookups hit the DB once per player, then cache.
            var discordIds = new Dictionary<NetUserId, ulong?>();
            foreach (var userId in reports.Select(r => r.UserId).OfType<NetUserId>().Distinct())
                discordIds[userId] = await _relay.GetLinkedDiscordId(userId);

            // The thread must exist before the followers post into it; delivery methods catch their
            // own errors, so nothing escapes this loop.
            var threadId = await _relay.CreateThread(HelpSource.News, opener);
            if (threadId == null)
            {
                _sawmill.Error($"News relay dropped round {context.RoundId}: news thread could not be created after retries");
                return;
            }

            foreach (var report in reports)
            {
                string? reporter = null;
                if (report.UserId is { } userId && discordIds.TryGetValue(userId, out var linked) && linked is { } id)
                    reporter = $"<@{id}>";
                else if (report.Ckey is { } ckey)
                    reporter = ckey;

                var fields = new List<HelpWebhookEmbedField>
                {
                    new() { Name = "Published", Value = report.Article.ShareTime.ToString(@"hh\:mm\:ss") },
                };
                if (reporter != null)
                    fields.Add(new() { Name = "Reporter", Value = reporter });

                _relay.Post(HelpSource.News, new HelpPost
                {
                    Title = report.Article.Title,
                    Description = FormattedMessage.RemoveMarkupPermissive(report.Article.Content),
                    Author = report.Article.Author ?? "unknown",
                    ThreadId = threadId,
                    OmitContextFields = true,
                    Context = context,
                    ExtraFields = fields,
                });
            }
        });
    }
}

/// <summary>
/// Raised from upstream's TryAddNews (CMU14 tag there): carries the publishing actor, which the
/// article itself never stores.
/// </summary>
[ByRefEvent]
public record struct StationNewsAuthoredEvent(EntityUid Station, NewsArticle Article, EntityUid? Actor);
