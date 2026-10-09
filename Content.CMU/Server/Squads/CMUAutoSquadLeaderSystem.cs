using Content.Server.Afk;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.CMU14.Squads;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Squads;

/// <summary>
/// Fills leadership gaps in GOVFOR and OPFOR squads:
/// <list type="bullet">
/// <item>A squad without a living squad leader for <see cref="SquadLeaderlessTime"/> has its highest
/// ranking member promoted to squad leader.</item>
/// <item>A fireteam without a living fireteam leader for <see cref="FireteamLeaderlessTime"/> has its
/// highest ranking member made fireteam leader.</item>
/// </list>
/// Only people who can lead (see <see cref="SquadLeaderTrackerSystem.CMUCanAutoLead"/>) are picked. A
/// leader who was replaced while dead gets their position back once they can lead again, as long as their
/// replacement is still the one in charge.
/// </summary>
public sealed partial class CMUAutoSquadLeaderSystem : EntitySystem
{
    [Dependency] private IAfkManager _afk = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedRankSystem _rank = default!;
    [Dependency] private SquadSystem _squad = default!;
    [Dependency] private SquadLeaderTrackerSystem _squadLeaderTracker = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan SquadLeaderlessTime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FireteamLeaderlessTime = TimeSpan.FromMinutes(7);
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(10);
    private static readonly HashSet<string> Groups = new(StringComparer.OrdinalIgnoreCase) { "GOVFOR", "OPFOR" };

    private readonly Dictionary<EntityUid, TimeSpan> _squadLeaderlessSince = new();
    private readonly Dictionary<(EntityUid Squad, int Fireteam), TimeSpan> _fireteamLeaderlessSince = new();

    // Leaders replaced while dead, so they can be restored once they can lead again.
    private readonly Dictionary<EntityUid, LeaderReplacement> _replacedSquadLeaders = new();
    private readonly Dictionary<(EntityUid Squad, int Fireteam), LeaderReplacement> _replacedFireteamLeaders = new();

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCanAutoLeadEvent>(OnCanAutoLead);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnCanAutoLead(ref CMUCanAutoLeadEvent args)
    {
        if (TryComp(args.Uid, out ActorComponent? actor) && _afk.IsAfk(actor.PlayerSession))
            args.Cancelled = true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _squadLeaderlessSince.Clear();
        _fireteamLeaderlessSince.Clear();
        _replacedSquadLeaders.Clear();
        _replacedFireteamLeaders.Clear();
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        if (time < _nextCheck)
            return;

        _nextCheck = time + CheckEvery;

        RestoreReplacedLeaders();

        var squads = EntityQueryEnumerator<SquadTeamComponent>();
        while (squads.MoveNext(out var uid, out var squad))
        {
            if (!Groups.Contains(squad.Group))
                continue;

            CheckSquadLeader((uid, squad), time);
            CheckFireteamLeaders((uid, squad), time);
        }

        foreach (var squad in _squadLeaderlessSince.Keys)
        {
            if (TerminatingOrDeleted(squad))
                _squadLeaderlessSince.Remove(squad);
        }

        foreach (var key in _fireteamLeaderlessSince.Keys)
        {
            if (TerminatingOrDeleted(key.Squad))
                _fireteamLeaderlessSince.Remove(key);
        }
    }

    private void RestoreReplacedLeaders()
    {
        foreach (var (squadUid, replacement) in _replacedSquadLeaders)
        {
            if (!TryGetOriginalInSquad(squadUid, replacement.Original, out var squad, out var member))
            {
                _replacedSquadLeaders.Remove(squadUid);
                continue;
            }

            if (!_squadLeaderTracker.CMUCanAutoLead(replacement.Original))
                continue;

            _replacedSquadLeaders.Remove(squadUid);
            var current = _squad.TryGetSquadLeader((squadUid, squad), out var leader) ? leader.Owner : (EntityUid?) null;
            if (current == null || current == replacement.Replacement)
                _squad.PromoteSquadLeader((replacement.Original, member), replacement.Original, squad.LeaderIcon);
        }

        foreach (var (key, replacement) in _replacedFireteamLeaders)
        {
            if (!TryGetOriginalInSquad(key.Squad, replacement.Original, out var squad, out _) ||
                _squadLeaderTracker.CMUGetFireteam(replacement.Original) != key.Fireteam)
            {
                _replacedFireteamLeaders.Remove(key);
                continue;
            }

            if (!_squadLeaderTracker.CMUCanAutoLead(replacement.Original))
                continue;

            _replacedFireteamLeaders.Remove(key);
            var fireteams = _squadLeaderTracker.CMUGetFireteams(squad);
            var current = key.Fireteam < fireteams.Length ? fireteams[key.Fireteam].Leader : null;
            if (current == null || current == replacement.Replacement)
                _squadLeaderTracker.CMUSetFireteamLeader((key.Squad, squad), key.Fireteam, replacement.Original, "revived and restored");
        }
    }

    /// <summary>
    /// Whether a replaced leader still exists and is still in the squad they led.
    /// </summary>
    private bool TryGetOriginalInSquad(
        EntityUid squadUid,
        EntityUid original,
        out SquadTeamComponent squad,
        out SquadMemberComponent member)
    {
        squad = default!;
        member = default!;
        return !TerminatingOrDeleted(original) &&
               TryComp(squadUid, out SquadTeamComponent? squadComp) &&
               TryComp(original, out SquadMemberComponent? memberComp) &&
               memberComp.Squad == squadUid &&
               (squad = squadComp) != null &&
               (member = memberComp) != null;
    }

    private void CheckSquadLeader(Entity<SquadTeamComponent> squad, TimeSpan time)
    {
        EntityUid? deadLeader = null;
        if (_squad.TryGetSquadLeader(squad, out var leader))
        {
            if (!_mobState.IsDead(leader))
            {
                _squadLeaderlessSince.Remove(squad);
                return;
            }

            deadLeader = leader;
        }

        if (!TimerElapsed(_squadLeaderlessSince, squad.Owner, time, SquadLeaderlessTime) ||
            PickHighestRanking(squad.Comp.Members) is not { } candidate ||
            !TryComp(candidate, out SquadMemberComponent? member))
        {
            return;
        }

        // Promotion announces the new leader to the squad and demotes a dead one.
        _squad.PromoteSquadLeader((candidate, member), candidate, squad.Comp.LeaderIcon);
        if (!HasComp<SquadLeaderComponent>(candidate))
            return;

        _squadLeaderlessSince.Remove(squad);
        if (deadLeader != null)
            _replacedSquadLeaders[squad] = new LeaderReplacement(deadLeader.Value, candidate);
    }

    private void CheckFireteamLeaders(Entity<SquadTeamComponent> squad, TimeSpan time)
    {
        var fireteams = _squadLeaderTracker.CMUGetFireteams(squad.Comp);
        for (var i = 0; i < fireteams.Length; i++)
        {
            var key = (squad.Owner, i);
            var (leader, members) = fireteams[i];
            if (members.Count == 0 || (leader != null && !_mobState.IsDead(leader.Value)))
            {
                _fireteamLeaderlessSince.Remove(key);
                continue;
            }

            if (!TimerElapsed(_fireteamLeaderlessSince, key, time, FireteamLeaderlessTime) ||
                PickHighestRanking(members) is not { } candidate)
            {
                continue;
            }

            _squadLeaderTracker.CMUSetFireteamLeader(squad, i, candidate, "fireteam had no living leader");
            _fireteamLeaderlessSince.Remove(key);
            if (leader != null)
                _replacedFireteamLeaders[key] = new LeaderReplacement(leader.Value, candidate);
        }
    }

    /// <summary>
    /// Starts the timer the first time a gap is seen, and reports whether it has run for long enough.
    /// </summary>
    private static bool TimerElapsed<TKey>(Dictionary<TKey, TimeSpan> since, TKey key, TimeSpan time, TimeSpan limit)
        where TKey : notnull
    {
        if (!since.TryGetValue(key, out var start))
        {
            since[key] = time;
            return false;
        }

        return time - start >= limit;
    }

    /// <summary>
    /// The highest ranking member who can lead, excluding the squad leader.
    /// </summary>
    private EntityUid? PickHighestRanking(IEnumerable<EntityUid> members)
    {
        EntityUid? best = null;
        var bestScore = int.MinValue;
        foreach (var member in members)
        {
            if (HasComp<SquadLeaderComponent>(member) ||
                !_squadLeaderTracker.CMUCanAutoLead(member))
            {
                continue;
            }

            var score = CMURankScore.FromPaygrade(_rank.GetRank(member)?.Paygrade);
            if (score > bestScore)
            {
                best = member;
                bestScore = score;
            }
        }

        return best;
    }

    private readonly record struct LeaderReplacement(EntityUid Original, EntityUid Replacement);
}
