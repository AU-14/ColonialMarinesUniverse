using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server.CMU14.Fob;

/// <summary>Observes actual FTL entry. Passenger counts never decide evacuation or victory.</summary>
public sealed partial class CMUFobFlightReportingSystem : EntitySystem
{
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private MultiDeckDropshipSystem _multiDeck = default!;
    [Dependency] private CMUFobReserveSystem _reserve = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FTLStartedEvent>(OnDeparture, after: [typeof(MultiDeckDropshipSystem)]);
        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnLanding, after: [typeof(CMUFobMapSystem)]);
    }

    private void OnLanding(ref DropshipLandedOnPlanetEvent args)
    {
        var ship = args.Dropship;
        if (!_reserve.Enabled || !_dropships.TryGetGridFaction(ship, out var faction)
            || !_reserve.TryGetNetwork(faction, out var network) || Transform(ship).MapUid is not { } map
            || _multiDeck.GetGroundMap(ship, map) != network.Comp.Map)
            return;
        var site = TryComp<CMUFobSiteComponent>(ship.Comp.Destination, out var destination) ? destination.Id : null;
        _reserve.RecordLanding(network, site);
    }

    private void OnDeparture(ref FTLStartedEvent args)
    {
        if (!_reserve.Enabled || !TryComp<DropshipComponent>(args.Entity, out var dropship)
            || !_dropships.TryGetGridFaction(args.Entity, out var faction)
            || !_reserve.TryGetNetwork(faction, out var network))
            return;
        Entity<DropshipComponent> ship = (args.Entity, dropship);
        // Resolve the same landing level as normal dropship handling, including upper-deck controllers.
        if (args.FromMapUid is not { } fromMap || _multiDeck.GetGroundMap(ship, fromMap) != network.Comp.Map)
            return;
        var grids = new HashSet<EntityUid> { ship };
        if (TryComp<MultiDeckDropshipComponent>(ship, out var decks))
            foreach (var grid in decks.Decks.Values) grids.Add(grid);
        var aboard = 0;
        var xenos = 0;
        var ground = 0;
        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out var mob, out var transform))
        {
            if (mob.CurrentState == MobState.Dead) continue;
            var boarded = transform.GridUid is { } grid && grids.Contains(grid);
            if (TryComp<MarineComponent>(uid, out var marine) && marine.Faction == faction)
            {
                if (boarded) aboard++;
                else if (transform.MapUid == network.Comp.Map) ground++;
            }
            if (boarded && HasComp<XenoComponent>(uid)) xenos++;
        }
        _reserve.RecordDeparture(network, aboard, xenos, ground, ship.Comp.HijackLandAt != null || ship.Comp.IsHumanHijack);
    }
}
