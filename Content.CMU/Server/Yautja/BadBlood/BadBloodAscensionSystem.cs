using Content.Server.CMU14.Threats;
using Content.Server.EUI;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// The Ascension Stone. Standing on it while the clan has no Leader offers the Lodge.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodAscensionStoneComponent : Component;

/// <summary>
/// FLOW-113, the ascension vote. When the Leader is gone and a Lodge stands, the clan votes for a new one:
/// candidates stand on the Ascension Stone, every living Bad Blood votes, most votes wins, ties go to the higher
/// kill tally and then to whoever stood first. The winner becomes the Leader and the fallen Leader's bracer
/// scuttles. Before the first Lodge, during founding grace, the offer goes out over the bracers instead.
/// <para>
/// The vote itself is <see cref="BadBloodCampRuleComponent.ActiveVote"/> on the rule entity, and its three timers
/// are fields of that component. Modelled on the Pathogen overmind vote (CMUBlightCoreSystem).
/// </para>
/// </summary>
public sealed partial class BadBloodAscensionSystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> LeaderJob = "AU14JobThreatLeader";
    private static readonly ProtoId<JobPrototype> MemberJob = "AU14JobThreatMember";
    private const string BracerSlot = "gloves";

    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private BadBloodScuttleSystem _scuttle = default!;
    [Dependency] private ThreatSystem _threat = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<ICommonSession, BadBloodAscensionVoteEui> _voteUis = new();
    private readonly Dictionary<ICommonSession, BadBloodAscensionPromptEui> _promptUis = new();
    private readonly List<EntityUid> _bracerScratch = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodLeaderGoneEvent>(OnLeaderGone);
        SubscribeLocalEvent<BadBloodLeaderChangedEvent>(OnLeaderChanged);
        SubscribeLocalEvent<BadBloodLodgeDestroyedEvent>(OnLodgeDestroyed);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<BadBloodAscensionStoneComponent, StepTriggerAttemptEvent>(OnStoneStepAttempt);
        SubscribeLocalEvent<BadBloodAscensionStoneComponent, StepTriggeredOffEvent>(OnStoneStepped);
        SubscribeLocalEvent<BadBloodAscensionStoneComponent, GetVerbsEvent<AlternativeVerb>>(OnStoneVerbs);
    }

    public override void Update(float frameTime)
    {
        if (!_rule.TryGetRule(out var rule) || rule.Comp.ActiveVote is not { } vote)
        {
            // the rule ended under a running vote
            if (_voteUis.Count > 0 || _promptUis.Count > 0)
                CloseUis();

            return;
        }

        // a Leader who only lost their connection is back
        if (!_rule.IsLeaderGone())
        {
            CancelVote("cmu-bad-blood-ascension-cancelled-leader");
            return;
        }

        if (!IsVoteGroundStanding(vote))
        {
            CancelVote(vote.FromBracer
                ? "cmu-bad-blood-ascension-cancelled-grace"
                : "cmu-bad-blood-ascension-cancelled-lodge");
            return;
        }

        if (_timing.CurTime >= vote.EndsAt)
            ResolveVote();
    }

    #region Opening and closing

    public bool IsVoteOpen()
    {
        return GetVote() != null;
    }

    public BadBloodAscensionVote? GetVote()
    {
        return _rule.TryGetRule(out var rule) ? rule.Comp.ActiveVote : null;
    }

    /// <summary>
    /// A vote can run: the clan has no Leader, and either a Lodge stands or it is still founding grace.
    /// </summary>
    public bool CanVoteRun()
    {
        return _rule.IsRuleActive() && _rule.IsLeaderGone() && (_rule.HasLodge() || _rule.InFoundingGrace());
    }

    /// <summary>
    /// Opens the vote. It opens by itself when the Leader is lost, and again when someone stands on the Stone
    /// after a vote lapsed. False if one is running or <see cref="CanVoteRun"/> is false.
    /// </summary>
    public bool TryOpenVote()
    {
        if (!_rule.TryGetRule(out var rule) || rule.Comp.ActiveVote != null || !CanVoteRun())
            return false;

        var now = _timing.CurTime;
        var vote = new BadBloodAscensionVote
        {
            StartedAt = now,
            EndsAt = now + rule.Comp.VoteDuration,
            FromBracer = !_rule.HasLodge(),
        };

        rule.Comp.ActiveVote = vote;

        _rule.AnnounceToClan(
            Loc.GetString(vote.FromBracer ? "cmu-bad-blood-ascension-opened-bracer" : "cmu-bad-blood-ascension-opened",
                ("seconds", (int) Math.Ceiling(rule.Comp.VoteDuration.TotalSeconds))),
            PopupType.LargeCaution);

        // no Stone to stand on yet: the offer goes to everyone over their bracer
        if (vote.FromBracer)
        {
            var clan = EntityQueryEnumerator<YautjaBadBloodComponent, ActorComponent>();
            while (clan.MoveNext(out var uid, out _, out var actor))
            {
                if (CanStand(uid, out _))
                    OpenPrompt(actor.PlayerSession, uid, true);
            }
        }

        return true;
    }

    /// <summary>
    /// Ends the running vote with no winner.
    /// </summary>
    public bool CancelVote(string reason = "cmu-bad-blood-ascension-cancelled-lodge")
    {
        if (!_rule.TryGetRule(out var rule) || rule.Comp.ActiveVote == null)
            return false;

        rule.Comp.ActiveVote = null;
        CloseUis();
        _rule.AnnounceToClan(Loc.GetString(reason), PopupType.MediumCaution);
        return true;
    }

    // with a Lodge the vote needs the Lodge. a bracer vote needs founding grace until a Lodge takes over
    private bool IsVoteGroundStanding(BadBloodAscensionVote vote)
    {
        if (_rule.HasLodge())
            return true;

        return vote.FromBracer && _rule.InFoundingGrace();
    }

    private void OnLeaderGone(ref BadBloodLeaderGoneEvent args)
    {
        TryOpenVote();
    }

    // someone else seated a Leader, e.g. an admin
    private void OnLeaderChanged(ref BadBloodLeaderChangedEvent args)
    {
        if (args.New == null || args.Rule.Comp.ActiveVote == null)
            return;

        args.Rule.Comp.ActiveVote = null;
        CloseUis();
    }

    private void OnLodgeDestroyed(ref BadBloodLodgeDestroyedEvent args)
    {
        if (args.Rule.Comp.ActiveVote is { FromBracer: false })
            CancelVote("cmu-bad-blood-ascension-cancelled-lodge");
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        CloseUis();
    }

    #endregion

    #region Candidates and votes

    /// <summary>
    /// Worthy kills of <paramref name="uid"/>: the tie-break, and what the vote window shows.
    /// </summary>
    public int GetKills(EntityUid uid)
    {
        return TryComp(uid, out BadBloodKillTallyComponent? tally) ? tally.Kills : 0;
    }

    /// <summary>
    /// Votes for <paramref name="candidate"/> from voters who are still alive.
    /// </summary>
    public int CountVotes(EntityUid candidate)
    {
        if (GetVote() is not { } vote)
            return 0;

        var votes = 0;
        foreach (var (voter, choice) in vote.Votes)
        {
            if (choice == candidate && IsLivingBadBlood(voter))
                votes++;
        }

        return votes;
    }

    /// <summary>
    /// Whether <paramref name="uid"/> may claim the Lodge right now: a living, conscious Bad Blood, while a vote
    /// can run, who is not a candidate already. <paramref name="reason"/> is a locale id.
    /// </summary>
    public bool CanStand(EntityUid uid, out string reason)
    {
        reason = "cmu-bad-blood-ascension-not-clan";
        if (!_badBlood.IsBadBlood(uid) || !_rule.IsRuleActive())
            return false;

        reason = "cmu-bad-blood-ascension-leader-lives";
        if (!_rule.IsLeaderGone())
            return false;

        reason = "cmu-bad-blood-ascension-no-lodge";
        if (!CanVoteRun())
            return false;

        reason = "cmu-bad-blood-ascension-not-fit";
        if (TerminatingOrDeleted(uid) || !_mobState.IsAlive(uid))
            return false;

        reason = "cmu-bad-blood-ascension-already-standing";
        return GetVote() is not { } vote || !vote.Candidates.Contains(uid);
    }

    /// <summary>
    /// <paramref name="uid"/> claims the Lodge. Opens a vote if none is running, and every candidate adds
    /// <see cref="BadBloodCampRuleComponent.VotePerCandidate"/> to it, up to
    /// <see cref="BadBloodCampRuleComponent.VoteMaxDuration"/> from its start. The Stone and the bracer offer
    /// only ask players, a caller that asks for a body nobody is driving gets what it asked for.
    /// </summary>
    public bool TryStandAsCandidate(EntityUid uid)
    {
        if (!CanStand(uid, out _) || !_rule.TryGetRule(out var rule))
            return false;

        if (rule.Comp.ActiveVote == null && !TryOpenVote())
            return false;

        if (rule.Comp.ActiveVote is not { } vote)
            return false;

        vote.Candidates.Add(uid);

        var latest = vote.StartedAt + rule.Comp.VoteMaxDuration;
        vote.EndsAt += rule.Comp.VotePerCandidate;
        if (vote.EndsAt > latest)
            vote.EndsAt = latest;

        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-ascension-candidate",
            ("name", Name(uid)),
            ("kills", GetKills(uid))));

        OpenVoteUis();
        return true;
    }

    /// <summary>
    /// A living Bad Blood votes for a candidate. Voting again moves the vote.
    /// </summary>
    public bool TryVote(EntityUid voter, EntityUid candidate)
    {
        if (GetVote() is not { } vote || !IsLivingBadBlood(voter) || !vote.Candidates.Contains(candidate))
            return false;

        vote.Votes[voter] = candidate;
        DirtyVoteUis();
        return true;
    }

    private bool IsLivingBadBlood(EntityUid uid)
    {
        return !TerminatingOrDeleted(uid) && _badBlood.IsBadBlood(uid) && !_mobState.IsDead(uid);
    }

    #endregion

    #region Resolution

    /// <summary>
    /// Who would win if the vote closed now: the living candidate with the most votes, then the most kills, then
    /// the one who stood first. Null with no living candidate.
    /// </summary>
    public EntityUid? GetFrontRunner()
    {
        if (GetVote() is not { } vote)
            return null;

        EntityUid? best = null;
        var bestVotes = -1;
        var bestKills = -1;

        // earliest first, and only a strictly better count takes the lead: the order is the last tie-break
        foreach (var candidate in vote.Candidates)
        {
            if (!IsLivingBadBlood(candidate))
                continue;

            var votes = CountVotes(candidate);
            var kills = GetKills(candidate);
            if (votes < bestVotes || votes == bestVotes && kills <= bestKills)
                continue;

            best = candidate;
            bestVotes = votes;
            bestKills = kills;
        }

        return best;
    }

    /// <summary>
    /// Closes the running vote now and seats the winner. Runs by itself when the vote's time is up. Null if the
    /// vote lapsed with no candidate, or the clan got its Leader or lost its Lodge in the meantime.
    /// </summary>
    public EntityUid? ResolveVote()
    {
        if (!_rule.TryGetRule(out var rule) || rule.Comp.ActiveVote is not { } vote)
            return null;

        // the same conditions the vote needed to open, checked again before anyone is crowned
        if (!_rule.IsLeaderGone())
        {
            CancelVote("cmu-bad-blood-ascension-cancelled-leader");
            return null;
        }

        if (!IsVoteGroundStanding(vote))
        {
            CancelVote(vote.FromBracer
                ? "cmu-bad-blood-ascension-cancelled-grace"
                : "cmu-bad-blood-ascension-cancelled-lodge");
            return null;
        }

        var winner = GetFrontRunner();
        var votes = winner is { } front ? CountVotes(front) : 0;

        rule.Comp.ActiveVote = null;
        CloseUis();

        if (winner is not { } chosen)
        {
            _rule.AnnounceToClan(Loc.GetString(vote.FromBracer
                ? "cmu-bad-blood-ascension-lapsed-bracer"
                : "cmu-bad-blood-ascension-lapsed"), PopupType.MediumCaution);

            // there is no Stone to step on in founding grace, so the offer goes out again
            if (vote.FromBracer)
                TryOpenVote();

            return null;
        }

        if (!Promote(rule, chosen))
            return null;

        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-ascension-won",
            ("name", Name(chosen)),
            ("votes", votes)), PopupType.Large);
        return chosen;
    }

    private bool Promote(Entity<BadBloodCampRuleComponent> rule, EntityUid winner)
    {
        var old = rule.Comp.Leader;

        // every leader bracer there is before the winner's own becomes one: those are the fallen Leader's
        _bracerScratch.Clear();
        var bracers = EntityQueryEnumerator<BadBloodLeaderBracerComponent>();
        while (bracers.MoveNext(out var bracer, out _))
        {
            _bracerScratch.Add(bracer);
        }

        // the Leader threat job, so the marines' kill-the-leader objective follows the new Leader
        _threat.ApplyThreatRole(winner, LeaderJob);
        if (old is { } fallen &&
            fallen != winner &&
            !TerminatingOrDeleted(fallen) &&
            TryComp(fallen, out ThreatComponent? threat) &&
            threat.ObjectiveJob == LeaderJob)
        {
            threat.ObjectiveJob = MemberJob;
        }

        if (!_rule.SetLeader(winner))
        {
            _bracerScratch.Clear();
            return false;
        }

        _inventory.TryGetSlotEntity(winner, BracerSlot, out var own);
        foreach (var bracer in _bracerScratch)
        {
            // a winner already wearing the old Leader's bracer keeps it
            if (bracer != own)
                _scuttle.TryStartScuttle(bracer, winner);
        }

        _bracerScratch.Clear();
        return true;
    }

    #endregion

    #region The Stone

    private void OnStoneStepAttempt(Entity<BadBloodAscensionStoneComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (CanStand(args.Tripper, out _) && HasComp<ActorComponent>(args.Tripper))
            args.Continue = true;
    }

    private void OnStoneStepped(Entity<BadBloodAscensionStoneComponent> ent, ref StepTriggeredOffEvent args)
    {
        OfferStone(args.Tripper, true);
    }

    // the same offer by hand, for someone already standing on the Stone when the Leader falls
    private void OnStoneVerbs(Entity<BadBloodAscensionStoneComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_badBlood.IsBadBlood(args.User) || !_rule.IsLeaderGone())
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-bad-blood-ascension-verb"),
            Message = Loc.GetString("cmu-bad-blood-ascension-verb-message"),
            Act = () => OfferStone(user, false),
        });
    }

    private void OfferStone(EntityUid user, bool quiet)
    {
        if (!_player.TryGetSessionByEntity(user, out var session))
            return;

        if (!CanStand(user, out var reason))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);

            return;
        }

        OpenPrompt(session, user, false);
    }

    #endregion

    #region Windows

    private void OpenPrompt(ICommonSession session, EntityUid candidate, bool fromBracer)
    {
        if (_promptUis.ContainsKey(session))
            return;

        var eui = new BadBloodAscensionPromptEui(this, candidate, fromBracer);
        _promptUis[session] = eui;
        _eui.OpenEui(eui, session);
    }

    internal void OnPromptAnswered(BadBloodAscensionPromptEui eui, bool accept)
    {
        // they may have died, or the clan may have its Leader, since the window opened
        if (accept && eui.Player.AttachedEntity == eui.Candidate)
            TryStandAsCandidate(eui.Candidate);
    }

    internal void OnPromptClosed(BadBloodAscensionPromptEui eui)
    {
        if (_promptUis.TryGetValue(eui.Player, out var open) && open == eui)
            _promptUis.Remove(eui.Player);
    }

    internal void OnVoteUiClosed(BadBloodAscensionVoteEui eui)
    {
        if (_voteUis.TryGetValue(eui.Player, out var open) && open == eui)
            _voteUis.Remove(eui.Player);
    }

    internal void OnVoteUiVoted(BadBloodAscensionVoteEui eui, NetEntity candidate)
    {
        if (eui.Player.AttachedEntity is { } voter && TryGetEntity(candidate, out var uid))
            TryVote(voter, uid.Value);
    }

    internal BadBloodAscensionVoteState BuildVoteState(ICommonSession session)
    {
        var state = new BadBloodAscensionVoteState();
        if (GetVote() is not { } vote)
            return state;

        state.EndsAt = vote.EndsAt;
        if (session.AttachedEntity is { } voter && vote.Votes.TryGetValue(voter, out var choice))
            state.VotedFor = GetNetEntity(choice);

        foreach (var candidate in vote.Candidates)
        {
            if (TerminatingOrDeleted(candidate))
                continue;

            state.Candidates.Add(new BadBloodAscensionCandidate(
                GetNetEntity(candidate),
                Name(candidate),
                GetKills(candidate),
                CountVotes(candidate)));
        }

        return state;
    }

    // every living Bad Blood with a player gets the vote window. hounds are not Bad Bloods and do not vote
    private void OpenVoteUis()
    {
        var clan = EntityQueryEnumerator<YautjaBadBloodComponent, ActorComponent>();
        while (clan.MoveNext(out var uid, out _, out var actor))
        {
            if (_mobState.IsDead(uid) || _voteUis.ContainsKey(actor.PlayerSession))
                continue;

            var eui = new BadBloodAscensionVoteEui(this);
            _voteUis[actor.PlayerSession] = eui;
            _eui.OpenEui(eui, actor.PlayerSession);
        }

        DirtyVoteUis();
    }

    private void DirtyVoteUis()
    {
        foreach (var eui in _voteUis.Values)
        {
            eui.StateDirty();
        }
    }

    private void CloseUis()
    {
        foreach (var eui in new List<BadBloodAscensionVoteEui>(_voteUis.Values))
        {
            if (!eui.IsShutDown)
                eui.Close();
        }

        foreach (var eui in new List<BadBloodAscensionPromptEui>(_promptUis.Values))
        {
            if (!eui.IsShutDown)
                eui.Close();
        }

        _voteUis.Clear();
        _promptUis.Clear();
    }

    #endregion
}
