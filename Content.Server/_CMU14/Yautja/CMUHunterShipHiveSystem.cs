using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared._CMU14.Xenonids.Hive;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

/// <summary>
/// Creates and scopes the two xeno hives used by the specimen displays on the Hunter Ship.
/// </summary>
public sealed class CMUHunterShipHiveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private XenoHiveSystem _hive = default!;

    private TimeSpan _nextRetry;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUHunterShipHiveBootstrapComponent, ComponentStartup>(OnBootstrapStartup);
        SubscribeLocalEvent<CMUHunterShipHiveBootstrapComponent, StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<CMUHunterShipHiveBootstrapComponent, EntityTerminatingEvent>(OnBootstrapTerminating);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextRetry)
            return;

        _nextRetry = _timing.CurTime + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<CMUHunterShipHiveBootstrapComponent>();
        while (query.MoveNext(out var uid, out var bootstrap))
        {
            EnsureHives((uid, bootstrap));
            AssignSpecimens((uid, bootstrap));
        }
    }

    private void OnBootstrapStartup(Entity<CMUHunterShipHiveBootstrapComponent> ent, ref ComponentStartup args)
    {
        EnsureHives(ent);
    }

    private void OnStationPostInit(Entity<CMUHunterShipHiveBootstrapComponent> ent, ref StationPostInitEvent args)
    {
        foreach (var grid in args.Station.Comp.Grids)
        {
            if (TryComp<TransformComponent>(grid, out var transform))
            {
                ent.Comp.RootMap = transform.MapID;
                break;
            }
        }

        EnsureHives(ent);
        AssignSpecimens(ent);
    }

    private void OnBootstrapTerminating(Entity<CMUHunterShipHiveBootstrapComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.AlphaHive is { } alpha && Exists(alpha))
            QueueDel(alpha);

        if (ent.Comp.ForsakenHive is { } forsaken && Exists(forsaken))
            QueueDel(forsaken);
    }

    private void EnsureHives(Entity<CMUHunterShipHiveBootstrapComponent> ent)
    {
        if (ent.Comp.AlphaHive is not { } alpha || !Exists(alpha))
        {
            ent.Comp.AlphaHive = _hive.CreateHive("Hunter Ship Alpha Hive", "CMUHunterShipAlphaHive");
        }

        if (ent.Comp.ForsakenHive is not { } forsaken || !Exists(forsaken))
        {
            ent.Comp.ForsakenHive = _hive.CreateHive("Hunter Ship Forsaken Hive", "CMUHunterShipForsakenHive");
        }

        TryResolveNetwork(ent);
    }

    private void TryResolveNetwork(Entity<CMUHunterShipHiveBootstrapComponent> ent)
    {
        if (ent.Comp.RootMap is not { } rootMap)
            return;

        var rootMapEntity = _map.GetMap(rootMap);
        if (!TryComp<CMUZLevelMapComponent>(rootMapEntity, out var rootZMap) ||
            !rootZMap.NetworkUid.IsValid())
        {
            return;
        }

        ent.Comp.Network = rootZMap.NetworkUid;
    }

    private void AssignSpecimens(Entity<CMUHunterShipHiveBootstrapComponent> ent)
    {
        if (ent.Comp.RootMap is not { } rootMap ||
            ent.Comp.AlphaHive is not { } alpha ||
            ent.Comp.ForsakenHive is not { } forsaken ||
            !Exists(alpha) ||
            !Exists(forsaken))
        {
            return;
        }

        TryResolveNetwork(ent);

        var maps = new HashSet<MapId> { rootMap };
        if (ent.Comp.Network is { } network)
        {
            var zMaps = EntityQueryEnumerator<CMUZLevelMapComponent, MapComponent>();
            while (zMaps.MoveNext(out _, out var zMap, out var map))
            {
                if (zMap.NetworkUid == network)
                    maps.Add(map.MapId);
            }
        }

        var assignments = EntityQueryEnumerator<CMUHunterShipHiveAssignmentComponent, TransformComponent>();
        while (assignments.MoveNext(out var uid, out var assignment, out var transform))
        {
            if (!maps.Contains(transform.MapID))
                continue;

            var hive = assignment.Hive == CMUHunterShipHiveKind.Alpha ? alpha : forsaken;
            _hive.SetHive(uid, hive);
        }
    }
}
