using Content.Server.GameTicking;
using Content.Server.CMU14.Dropship;
using Content.Server.CMU14.ForceOnForce;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Round;
using Content.Shared.GameTicking;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.WeedKiller;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;

namespace Content.Server._RMC14.Dropship;

public sealed partial class DropshipSystem
{
    private static readonly TimeSpan ForceOnForceLaunchDelay = TimeSpan.FromMinutes(3);

    // Keep the expired deadline until round cleanup. Losing the first ship or missing
    // its departure must never start a second opening deployment.
    private TimeSpan? _openingDeparture;

    protected override void OnForceOnForceRoundRestart(RoundRestartCleanupEvent args)
    {
        base.OnForceOnForceRoundRestart(args);
        _openingDeparture = null;
    }

    private bool IsDeploymentConsole(EntityUid computer)
    {
        if (!IsForceOnForcePreset())
            return false;
        return HasComp<PlatoonAircraftComponent>(Transform(computer).GridUid) ||
               TryComp<WhitelistedShuttleComponent>(computer, out var whitelist) &&
               ForceOnForceSystem.Opponent(whitelist.Faction) != null;
    }

    private TimeSpan? GetOpeningDeparture(EntityUid computer, DropshipComponent ship, EntityUid destination)
    {
        if (_openingDeparture <= _timing.CurTime || !IsDeploymentConsole(computer) ||
            !HasComp<DropshipDestinationComponent>(destination))
            return null;

        var planet = EntityManager.System<RMCPlanetSystem>();
        if (!planet.IsOnPlanetLevel(Transform(destination)))
        {
            // A flyby or pad-to-pad trip also lets passengers jump from hyperspace.
            // The occupied pad identifies the carrier even on a map shared by two fleets.
            var source = Transform(computer);
            var onCarrier = GetCarrierFaction(computer) != null ||
                ship.Destination is { } pad && !TerminatingOrDeleted(pad) &&
                Transform(pad).MapUid is { } padMap && _zLevels.IsSameZNetwork(source.MapUid, padMap) && GetCarrierFaction(pad) != null;
            if (!onCarrier || planet.IsOnPlanetLevel(source))
                return null;
        }

        return _openingDeparture ?? _timing.CurTime + ForceOnForceLaunchDelay;
    }

    private void QueueOpeningDeparture(EntityUid dropship, FTLComponent flight, TimeSpan departure, EntityUid? origin)
    {
        // FTL accepts float seconds. Pin the absolute end so late joiners depart on the same tick.
        flight.StateTime.End = departure;
        flight.OpeningDeployment = true;
        flight.OpeningDeploymentOrigin = origin;
        Dirty(dropship, flight);
        if (_openingDeparture == null)
        {
            _openingDeparture = departure;
            var message = Loc.GetString("cmu-fof-launch-preparing", ("minutes", (int) ForceOnForceLaunchDelay.TotalMinutes));
            _marineAnnounce.AnnounceARES(dropship, message, faction: "govfor");
            _marineAnnounce.AnnounceARES(dropship, message, faction: "opfor");
        }
    }

    private void OnOpeningFlightRemoved(Entity<FTLComponent> flight, ref ComponentRemove args)
    {
        if (!flight.Comp.OpeningDeployment || flight.Comp.State != FTLState.Starting ||
            TerminatingOrDeleted(flight.Owner) || !TryComp<DropshipComponent>(flight.Owner, out var ship))
            return;

        var origin = flight.Comp.OpeningDeploymentOrigin;
        if (ship.Destination is { } target && target != origin &&
            TryComp<DropshipDestinationComponent>(target, out var pad) && pad.Ship == flight.Owner)
        {
            SetDestinationShip(target, null);
            if (HasComp<EphemeralDropshipDestinationComponent>(target))
                QueueDel(target);
        }

        // A different aircraft may have booked the old pad in the meantime.
        if (origin is { } home && !TerminatingOrDeleted(home) &&
            TryComp<DropshipDestinationComponent>(home, out var homePad) &&
            (homePad.Ship == null || homePad.Ship == flight.Owner))
            SetDestinationShip(home, flight.Owner);
        else
            origin = null;

        ship.Destination = origin;
        ship.State = FTLState.Available;
        Dirty(flight.Owner, ship);
        RemCompDeferred<DropshipInFlyByComponent>(flight.Owner);
        RefreshUI();
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

    private bool IsForceOnForcePreset() =>
        EntityManager.System<GameTicker>().CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) == true;

    protected override bool IsForceOnForceHuman(EntityUid user) =>
        IsForceOnForcePreset()
        && !HasComp<XenoComponent>(user)
        && EntityManager.System<ForceOnForceSystem>().GetFaction(user) != null;

    protected override bool IsForceOnForceRound() => IsForceOnForcePreset();

    private bool CanLandAt(EntityUid computer, EntityUid destination)
    {
        if (Transform(computer).GridUid is { } grid)
        {
            var permission = new CMUDropshipLandingPermissionEvent(destination);
            RaiseLocalEvent(grid, ref permission);
            if (permission.Allowed)
                return true;
        }

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
