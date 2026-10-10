using Content.Server.GameTicking;
using Content.Server.CMU14.ForceOnForce;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.Round;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.WeedKiller;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;

namespace Content.Server._RMC14.Dropship;

public sealed partial class DropshipSystem
{
    private static readonly TimeSpan ForceOnForceLaunchDelay = TimeSpan.FromMinutes(3);

    private bool TryGetForceOnForceLaunchWindow(EntityUid computer, EntityUid destination, bool hijack,
        out string enemy, out TimeSpan departure, out bool newWindow)
    {
        enemy = string.Empty;
        departure = default;
        newWindow = true;
        if (hijack || _gameTicker.CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) != true ||
            !TryComp<WhitelistedShuttleComponent>(computer, out var whitelist) ||
            ForceOnForceSystem.Opponent(whitelist.Faction) is not { } opponent ||
            !TryComp<DropshipDestinationComponent>(destination, out _) ||
            (!HasComp<RMCPlanetComponent>(Transform(destination).MapUid) &&
             !HasComp<RMCPlanetComponent>(Transform(destination).GridUid)))
            return false;

        enemy = opponent;
        departure = _timing.CurTime + ForceOnForceLaunchDelay;
        var pending = EntityQueryEnumerator<ForceOnForceLaunchComponent, FTLComponent>();
        while (pending.MoveNext(out var uid, out _, out var flight))
        {
            if (TerminatingOrDeleted(uid) || flight.State != FTLState.Starting || flight.StateTime.End <= _timing.CurTime)
                continue;
            if (flight.StateTime.End < departure)
                departure = flight.StateTime.End;
            newWindow = false;
        }
        return true;
    }

    private void FinishForceOnForceLaunchWindow(EntityUid dropship, TimeSpan departure, string enemy, bool newWindow)
    {
        if (!TryComp<FTLComponent>(dropship, out var flight) || flight.State != FTLState.Starting)
            return;

        EnsureComp<ForceOnForceLaunchComponent>(dropship);
        // Preserve the exact shared deadline; float startup seconds can round to different ticks.
        flight.StateTime.End = departure;
        Dirty(dropship, flight);
        RefreshUI();
        if (newWindow)
            _marineAnnounce.AnnounceARES(dropship, Loc.GetString("cmu-fof-launch-preparing"), faction: enemy);
    }

    private void OnForceOnForceWeedKillerAttempt(ref WeedKillerDeployAttemptEvent args)
    {
        if (_gameTicker.CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) == true)
            args.Cancelled = true;
    }

    private void AnnounceForceOnForceBoarders(Entity<DropshipComponent> dropship)
    {
        if (EntityManager.System<GameTicker>().CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) != true ||
            !TryGetDropshipNavigationComputer(dropship, out var computer) ||
            !TryComp<WhitelistedShuttleComponent>(computer.Owner, out var shuttle) ||
            ForceOnForceSystem.Opponent(shuttle.Faction) is not { } enemy)
            return;

        var count = 0;
        var query = EntityQueryEnumerator<MarineComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var marine, out var mob))
        {
            if (mob.CurrentState != MobState.Dead && string.Equals(marine.Faction, enemy, StringComparison.OrdinalIgnoreCase) &&
                TryGetGridDropship(uid, out var boarded) && boarded.Owner == dropship.Owner)
                count++;
        }

        if (count == 0)
            return;

        _marineAnnounce.AnnounceARES(dropship,
            Loc.GetString("cmu-fof-hostile-boarders", ("name", Name(dropship)), ("count", count)),
            dropship.Comp.UnidentifledlifesignsSound, shuttle.Faction);
    }

    protected override bool IsForceOnForceHijacker(EntityUid computer, EntityUid user)
    {
        var faction = EntityManager.System<ForceOnForceSystem>();
        return IsForceOnForceHuman(user) &&
            faction.CanCommand(user, includeSquadLeaders: true) &&
            TryComp<WhitelistedShuttleComponent>(computer, out var shuttle) &&
            ForceOnForceSystem.Opponent(faction.GetFaction(user)) == shuttle.Faction?.ToLowerInvariant();
    }

    protected override bool IsForceOnForceHuman(EntityUid user) =>
        EntityManager.System<GameTicker>().CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) == true &&
        !HasComp<XenoComponent>(user) && EntityManager.System<ForceOnForceSystem>().GetFaction(user) != null;

    private bool CanLandAt(EntityUid computer, EntityUid destination)
    {
        if (!TryComp<WhitelistedShuttleComponent>(computer, out var shuttle))
            return GetCarrierFaction(destination) == null;

        var carrierFaction = GetCarrierFaction(destination);
        var controller = TryComp<DropshipDestinationComponent>(destination, out var landing)
            ? landing.FactionController
            : null;
        return (string.IsNullOrWhiteSpace(carrierFaction) || string.Equals(carrierFaction, shuttle.Faction, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(controller) || string.Equals(controller, shuttle.Faction, StringComparison.OrdinalIgnoreCase));
    }
}
