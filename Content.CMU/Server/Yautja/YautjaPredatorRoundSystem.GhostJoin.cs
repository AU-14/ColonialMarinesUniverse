using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja;

// cmss13 cm_initialize.dm check_predator_late_join: whitelisted, a hunt is on, never a pred before this round, and
// ordinary ranks need a free slot (leaders/council/ancients don't count)
public sealed partial class YautjaPredatorRoundSystem
{
    [Dependency] private IBanManager _bans = default!;

    private readonly HashSet<NetUserId> _huntersThisRound = new();

    private void InitializeGhostJoin()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnHunterSpawnComplete);
    }

    private void OnHunterSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId is not { } job || !TryGetActivePredatorRule(out var rule))
            return;

        if (job == rule.Comp.PredatorJob.Id || job == AncientJob.Id)
            _huntersThisRound.Add(ev.Player.UserId);
    }

    public bool WasHunterThisRound(NetUserId user) => _huntersThisRound.Contains(user);

    // whitelist + hunt only, so the action shows up. the rest is checked when it's pressed
    public bool CanSeeGhostJoinHunt(ICommonSession session)
    {
        return GameTicker.RunLevel == GameRunLevel.InRound &&
               IsHuntActive() &&
               CanUseHuntWhitelist(session);
    }

    public bool CanGhostJoinHunt(ICommonSession session, out string reason)
    {
        reason = string.Empty;
        if (!CanUseHuntWhitelist(session))
        {
            reason = Loc.GetString("cmu-yautja-ghost-join-hunt-not-whitelisted");
            return false;
        }

        if (GameTicker.RunLevel != GameRunLevel.InRound || !TryGetActivePredatorRule(out var rule))
        {
            reason = Loc.GetString("cmu-yautja-ghost-join-hunt-no-hunt");
            return false;
        }

        if (_huntersThisRound.Contains(session.UserId))
        {
            reason = Loc.GetString("cmu-yautja-ghost-join-hunt-already");
            return false;
        }

        var job = rule.Comp.PredatorJob;
        var disallowed = new HashSet<ProtoId<JobPrototype>>();
        var disallowedEv = new GetDisallowedJobsEvent(session, disallowed);
        RaiseLocalEvent(ref disallowedEv);
        if (_bans.GetJobBans(session.UserId) is { } bans)
            disallowed.UnionWith(bans);

        var rank = ResolveRankForSession(session);
        var bypass = GetRankSpawnPolicy(rank).BypassSlotCap;
        if (!bypass && disallowed.Contains(job))
        {
            // our own GetDisallowedJobs adds the hunter job when the last slots are held for leaders
            reason = Loc.GetString("cmu-yautja-ghost-join-hunt-full");
            return false;
        }

        if (!bypass &&
            (!TryGetHunterSlots(job, rule.Comp.HunterShipMap, out var available) ||
             available is <= 0 ||
             IsHunterSlotReservedForOrdinaryRank(available, rule.Comp.RankBypassSlotsRemaining)))
        {
            reason = Loc.GetString("cmu-yautja-ghost-join-hunt-full");
            return false;
        }

        if (GetPredatorStations(job, rule.Comp.HunterShipMap).Count == 0)
        {
            reason = Loc.GetString("cmu-yautja-ghost-join-hunt-no-hunt");
            return false;
        }

        return true;
    }

    public bool TryGhostJoinHunt(ICommonSession session, out string reason)
    {
        if (!CanGhostJoinHunt(session, out reason) || !TryGetActivePredatorRule(out var rule))
            return false;

        var ghost = session.AttachedEntity;
        var station = GetPredatorStations(rule.Comp.PredatorJob, rule.Comp.HunterShipMap).First();

        // same path as the lobby Join Hunt card, so profile, rank and spawn point all go through the checks we trust
        GameTicker.MakeJoinGame(session, station, rule.Comp.PredatorJob);

        if (session.AttachedEntity == ghost)
        {
            reason = Loc.GetString("cmu-yautja-ghost-join-hunt-failed");
            return false;
        }

        // the old ghost doesn't own the new mind, don't leave it floating around
        if (ghost is { } oldGhost && !TerminatingOrDeleted(oldGhost) && HasComp<GhostComponent>(oldGhost))
            QueueDel(oldGhost);

        return true;
    }
}
