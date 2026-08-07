using System.Text.RegularExpressions;
using Content.Server.Popups;
using Content.Server.Roles.Jobs;
using Content.Shared._AU14.Insurgency.Orders;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Insurgency.Orders;

/// <summary>
///     The cell leader writes their intent on a sheet once and passes the word. From then on
///     it sits in every cell member's character brief, including anyone recruited later.
///
///     Deliberately not an item drop: the orders live in the brief, not on paper, so they
///     cannot be looted off a body, faxed, or found by a patrol searching the right satchel.
/// </summary>
public sealed partial class CLFStandingOrdersSystem : EntitySystem
{
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Regex Whitespace = new(@"[ \t]+", RegexOptions.Compiled);

    /// <summary>
    ///     What is standing this round, or null if nothing has been passed down yet.
    /// </summary>
    public string? Orders { get; private set; }

    /// <summary>
    ///     Who issued it, for the byline on the brief.
    /// </summary>
    public string? IssuedBy { get; private set; }

    // budget and cooldown are round state, not sheet state - otherwise a second pad is a
    // second allowance and the whole thing becomes a chat channel with extra steps
    private int _issues;
    private TimeSpan _nextIssue;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CLFStandingOrdersComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        Orders = null;
        IssuedBy = null;
        _issues = 0;
        _nextIssue = TimeSpan.Zero;
    }

    private void OnGetVerbs(Entity<CLFStandingOrdersComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!CanIssue(ent, args.User))
            return;

        var user = args.User;
        var blocked = GetBlockReason(ent);

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("clf-standing-orders-verb"),
            Message = blocked ?? Loc.GetString(
                "clf-standing-orders-verb-tooltip",
                ("left", Math.Max(ent.Comp.MaxIssues - _issues, 0))),
            Disabled = blocked != null,
            Act = () => TryIssue(ent, user),
        });
    }

    /// <summary>
    ///     Why the word cannot go round right now, or null if it can.
    /// </summary>
    private string? GetBlockReason(Entity<CLFStandingOrdersComponent> ent)
    {
        if (_issues >= ent.Comp.MaxIssues)
            return Loc.GetString("clf-standing-orders-spent");

        var remaining = _nextIssue - _timing.CurTime;

        if (remaining <= TimeSpan.Zero)
            return null;

        return remaining < TimeSpan.FromMinutes(1)
            ? Loc.GetString("clf-standing-orders-cooldown-soon")
            : Loc.GetString(
                "clf-standing-orders-cooldown",
                ("minutes", (int) Math.Ceiling(remaining.TotalMinutes)));
    }

    private bool CanIssue(Entity<CLFStandingOrdersComponent> ent, EntityUid user)
    {
        if (!HasComp<CLFMemberComponent>(user))
            return false;

        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;

        foreach (var job in ent.Comp.AuthorJobs)
        {
            if (_jobs.MindHasJobWithId(mindId, job.Id))
                return true;
        }

        return false;
    }

    private void TryIssue(Entity<CLFStandingOrdersComponent> ent, EntityUid user)
    {
        // re-check on act: the verb list is built a tick before the click lands, and the
        // sheet can change hands or the cooldown can lapse in between
        if (!CanIssue(ent, user))
            return;

        if (GetBlockReason(ent) is { } blocked)
        {
            _popup.PopupEntity(blocked, user, user, PopupType.SmallCaution);
            return;
        }

        if (!TryComp(ent.Owner, out PaperComponent? paper))
            return;

        var orders = Tidy(paper.Content, ent.Comp.MaxLength);

        if (string.IsNullOrEmpty(orders))
        {
            // a blank sheet costs nothing - the budget is for what actually went round
            _popup.PopupEntity(Loc.GetString("clf-standing-orders-blank"), user, user, PopupType.SmallCaution);
            return;
        }

        var reissued = Orders != null;

        Orders = orders;
        IssuedBy = Name(user);

        _issues++;
        _nextIssue = _timing.CurTime + ent.Comp.Cooldown;

        _popup.PopupEntity(Loc.GetString("clf-standing-orders-issued"), user, user, PopupType.Medium);

        // everyone already in the cell hears it come round. anyone recruited later picks
        // it up from their brief instead
        var message = Loc.GetString(
            reissued ? "clf-standing-orders-notify-changed" : "clf-standing-orders-notify",
            ("orders", orders));

        var query = EntityQueryEnumerator<CLFMemberComponent>();

        while (query.MoveNext(out var member, out _))
        {
            _cmChat.ChatMessageToOne(message, member);
        }
    }

    /// <summary>
    ///     Flattens the sheet's line breaks and runs of whitespace into one plain line. The
    ///     brief carries what was said, not the leader's paper layout.
    /// </summary>
    private static string Tidy(string content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var text = content.Replace("\r\n", "\n").Replace('\r', '\n');
        text = Whitespace.Replace(text, " ");

        var lines = new List<string>();

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length > 0)
                lines.Add(trimmed);
        }

        var joined = string.Join(" ", lines);

        return joined.Length > maxLength
            ? joined[..maxLength].TrimEnd() + "..."
            : joined;
    }
}
