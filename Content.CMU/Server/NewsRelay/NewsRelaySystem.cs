using System.Linq;
using System.Threading.Tasks;
using Content.Server.CMU14.HelpRelay;
using Content.Shared.GameTicking;
using Content.Shared.MassMedia.Components;
using Content.Shared.MassMedia.Systems;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.NewsRelay;

/// <summary>
/// Delivers the round's station news to Discord at round end: one forum thread per round, one
/// message per article inside it, ordered by publish time. Upstream's own webhook path
/// (discord.news_webhook) stays dormant; this is the CMU replacement, riding the help relay.
/// </summary>
public sealed class NewsRelaySystem : EntitySystem
{
    [Dependency] private readonly HelpRelayManager _relay = default!;
    [Dependency] private readonly ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        _sawmill = _log.GetSawmill("newsrelay");
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEndMessage);
    }

    private void OnRoundEndMessage(RoundEndMessageEvent ev)
    {
        if (!_relay.IsEnabled(HelpSource.News))
            return;

        // Round state dies with the round, so articles and context are gathered on the main thread
        // before any delivery happens in the background.
        var articles = new List<NewsArticle>();
        var query = EntityQueryEnumerator<StationNewsComponent>();
        while (query.MoveNext(out _, out var comp))
            articles.AddRange(comp.Articles);

        if (articles.Count == 0)
            return;

        var ordered = articles.OrderBy(a => a.ShareTime).ToList();
        var context = _relay.GetContext();
        var opener = new HelpPost
        {
            Title = "Station news",
            Description = $"**{ordered.Count} article{(ordered.Count == 1 ? "" : "s")} published this round.**\n"
                + string.Join("\n", ordered.Select(a => $"• {a.Title}")),
            Author = "round digest",
            ThreadName = $"Round {context.RoundId} · {context.Gamemode ?? "?"} · {context.Map ?? "?"}",
            Context = context,
        };

        Task.Run(async () =>
        {
            // The thread must exist before the followers post into it; delivery methods catch their
            // own errors, so nothing escapes this loop.
            var threadId = await _relay.CreateThread(HelpSource.News, opener);
            if (threadId == null)
            {
                _sawmill.Error($"News relay dropped round {context.RoundId}: news thread could not be created after retries");
                return;
            }

            foreach (var article in ordered)
            {
                _relay.Post(HelpSource.News, new HelpPost
                {
                    Title = article.Title,
                    Description = FormattedMessage.RemoveMarkupPermissive(article.Content),
                    Author = article.Author ?? "unknown",
                    ThreadId = threadId,
                    OmitContextFields = true,
                    Context = context,
                    ExtraFields =
                    [
                        new() { Name = "Published", Value = article.ShareTime.ToString(@"hh\:mm\:ss") },
                    ],
                });
            }
        });
    }
}
