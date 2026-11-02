using System.Numerics;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Fob;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fob;

/// <summary>Authored map deployment and relay selection for the optional Distress Signal FOB pilot.</summary>
public sealed partial class CMUFobMapSystem : EntitySystem
{
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private MultiDeckDropshipSystem _multiDeck = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private CMUFobReserveSystem _reserve = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _pendingMaps = new();
    private bool _configured;
    private bool _enabled;
    private TimeSpan _nextUpdate;

    public bool PilotPrepared => _enabled && _reserve.Enabled && _contestedEvacuation;

    public override void Initialize()
    {
        InitializeEvacuation();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting, after: [typeof(CMUFobReserveSystem)]);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
        SubscribeLocalEvent<CMUFobMapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUFobLegacyDefenseComponent, MapInitEvent>(OnLegacyMapInit);
        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnLanded);
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        if (_configured)
            return;
        _configured = true;
        _enabled = _reserve.Enabled && _ticker.CurrentPreset?.ID == "DistressSignal";
        if (!_enabled)
            return;
        var maps = AllEntityQuery<CMUFobMapComponent>();
        while (maps.MoveNext(out var uid, out _))
            _pendingMaps.Add(uid);
    }

    private void OnCleanup(RoundRestartCleanupEvent args)
    {
        _pendingMaps.Clear();
        _contestedEvacuation = false;
        _configured = false;
        _enabled = false;
        _nextUpdate = TimeSpan.Zero;
    }

    private void OnMapInit(Entity<CMUFobMapComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Initialized = true;
        if (_enabled)
            _pendingMaps.Add(ent);
    }

    private bool AnchorOnGrid(EntityUid uid, Entity<MapGridComponent> grid)
    {
        return HasComp<CMUFobMapAnchorComponent>(uid) && !TerminatingOrDeleted(uid)
            && Transform(uid).GridUid == grid.Owner && Transform(uid).Anchored
            && !_map.GetTileRef(grid, grid.Comp, Transform(uid).Coordinates).Tile.IsEmpty;
    }

    private bool Validate(Entity<CMUFobMapComponent> ent)
    {
        if (!TryComp<MapGridComponent>(ent, out var grid) || ent.Comp.Sites.Count == 0
            || ent.Comp.IntermediateAfter <= TimeSpan.Zero || ent.Comp.FarAfter <= ent.Comp.IntermediateAfter)
            return false;
        var ids = new HashSet<string>();
        var relays = new HashSet<EntityUid>();
        foreach (var uid in ent.Comp.Sites)
        {
            if (!TryComp<CMUFobSiteComponent>(uid, out var site) || !TryComp<DropshipDestinationComponent>(uid, out var destination)
                || Transform(uid).GridUid != ent.Owner || string.IsNullOrWhiteSpace(site.Id) || !ids.Add(site.Id)
                || site.Faction != "govfor" || !destination.CanBePrimary || destination.Home
                || destination.FactionController != string.Empty && destination.FactionController != site.Faction
                || site.UtilityExclusions.Count == 0)
                return false;
            var anchors = new HashSet<EntityUid>();
            foreach (var route in site.Relays)
            {
                if (!anchors.Add(route.Anchor) || !AnchorOnGrid(route.Anchor, (ent, grid))
                    || string.IsNullOrWhiteSpace(route.Approach)
                    || string.IsNullOrWhiteSpace(Comp<CMUFobMapAnchorComponent>(route.Anchor).RelayName))
                    return false;
                relays.Add(route.Anchor);
            }
            foreach (var tier in Enum.GetValues<CMUFobRelayTier>())
            {
                var count = 0;
                var approaches = new HashSet<string>();
                foreach (var route in site.Relays)
                {
                    if (route.Tier != tier)
                        continue;
                    count++;
                    approaches.Add(route.Approach);
                }
                if (count < 4 || approaches.Count < 2)
                    return false;
            }
        }
        // Keep patrol objectives beyond FOB deployment and the sensors' initial seven-tile acquisition reach.
        const float relayClearance = CMUFobNetworkComponent.DefaultDeploymentRange + 10;
        foreach (var relay in relays)
        {
            foreach (var site in ent.Comp.Sites)
            {
                if (Vector2.DistanceSquared(_transform.GetWorldPosition(site), _transform.GetWorldPosition(relay)) <= relayClearance * relayClearance)
                    return false;
            }
        }
        return true;
    }

    private bool TryPrepare(Entity<CMUFobMapComponent> ent)
    {
        if (!_enabled || !_reserve.Enabled || !ent.Comp.Initialized || ent.Comp.Rejected || TerminatingOrDeleted(ent))
            return false;
        if (ent.Comp.Prepared)
            return true;
        if (!Validate(ent))
        {
            ent.Comp.Rejected = true;
            Log.Warning($"FOB configuration rejected on {ToPrettyString(ent)}; legacy defenses retained.");
            return false;
        }
        ent.Comp.Prepared = true;
        _contestedEvacuation = true;
        var supplies = new CMUFobSupplyAvailableEvent();
        RaiseLocalEvent(ref supplies);
        var map = Transform(ent).MapUid;
        var legacy = AllEntityQuery<CMUFobLegacyDefenseComponent, TransformComponent>();
        var removed = 0;
        while (legacy.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != map)
                continue;
            QueueDel(uid);
            removed++;
        }
        Log.Info($"FOB map prepared on {ToPrettyString(ent)}; removed {removed} legacy LZ defenses across {ent.Comp.Sites.Count} sites.");
        return true;
    }

    private void OnLegacyMapInit(Entity<CMUFobLegacyDefenseComponent> ent, ref MapInitEvent args)
    {
        if (!_enabled || !_reserve.Enabled)
            return;
        var maps = AllEntityQuery<CMUFobMapComponent, TransformComponent>();
        while (maps.MoveNext(out _, out var map, out var xform))
        {
            if (map.Prepared && xform.MapUid == Transform(ent).MapUid)
            {
                QueueDel(ent);
                return;
            }
        }
    }

    private void OnLanded(ref DropshipLandedOnPlanetEvent args)
    {
        var ship = args.Dropship;
        if (!_enabled || !_reserve.Enabled || ship.Comp.Crashed || ship.Comp.HijackLandAt != null
            || ship.Comp.IsHumanHijack || ship.Comp.Destination is not { } destination
            || !TryComp<CMUFobSiteComponent>(destination, out var site)
            || !_dropships.TryGetGridFaction(ship, out var faction) || faction != site.Faction
            || Transform(destination).GridUid is not { } grid || !TryComp<CMUFobMapComponent>(grid, out var config)
            || !config.Sites.Contains(destination) || !TryPrepare((grid, config))
            || Transform(ship).MapUid is not { } shipMap
            || _multiDeck.GetGroundMap(ship, shipMap) != Transform(destination).MapUid
            || site.Network != null)
            return;

        // The reserve's faction tombstone also rejects repeated/alternate LZ landings after hardware deletion.
        if (!_reserve.TryCreateNetwork(destination, faction, out var network, site.Id))
            return;
        site.Network = network;
        foreach (var route in site.Relays)
        {
            var anchor = Comp<CMUFobMapAnchorComponent>(route.Anchor);
            if (anchor.Relay != null)
                continue;
            anchor.Relay = SpawnAtPosition("CMUFobRelay", Transform(route.Anchor).Coordinates);
            _metadata.SetEntityName(anchor.Relay.Value, Loc.GetString(anchor.RelayName!));
        }
        Log.Info($"FOB {faction} selected {site.Id} on first friendly landing.");
        OfferRelays((destination, site), network, config);
    }

    private bool Available(CMUFobRelayRoute route, EntityUid map, out EntityUid relay, out string reason)
    {
        relay = default;
        reason = "missing endpoint";
        if (!TryComp<CMUFobMapAnchorComponent>(route.Anchor, out var anchor) || anchor.Relay is not { } uid
            || TerminatingOrDeleted(uid) || !TryComp<CMUFobRelayComponent>(uid, out var endpoint))
            return false;
        reason = "paused";
        if (MetaData(uid).EntityPaused || MetaData(route.Anchor).EntityPaused) return false;
        reason = "moved or unanchored";
        if (Transform(uid).MapUid != map || !Transform(uid).Anchored
            || Transform(route.Anchor).MapUid != map || !Transform(route.Anchor).Anchored
            || Vector2.DistanceSquared(_transform.GetWorldPosition(uid), _transform.GetWorldPosition(route.Anchor)) > 0.01f)
            return false;
        reason = "already assigned";
        if (endpoint.Network != null) return false;
        reason = $"reuse cooldown until {endpoint.ReusableAt}";
        if (_timing.CurTime < endpoint.ReusableAt) return false;
        reason = string.Empty;
        relay = uid;
        return true;
    }

    public bool TryGetRelayFailure(EntityUid network, out TimeSpan retryAt)
    {
        retryAt = default;
        if (!PilotPrepared || !TryComp<CMUFobNetworkComponent>(network, out var bank) || bank.JobExpiresAt != null)
            return false;
        var sites = EntityQueryEnumerator<CMUFobSiteComponent>();
        while (sites.MoveNext(out _, out var site))
        {
            if (site.Network != network || !site.NoRelayPair) continue;
            retryAt = site.NextOfferAttempt;
            return true;
        }
        return false;
    }

    /// <summary>Reserve utilities cannot obstruct the selected site's landing footprint.</summary>
    public bool CanPlaceUtility(EntityUid network, Vector2 worldPosition)
    {
        var sites = EntityQueryEnumerator<CMUFobSiteComponent>();
        while (sites.MoveNext(out var uid, out var site))
        {
            if (site.Network != network || Transform(uid).GridUid is not { } grid) continue;
            var local = Vector2.Transform(worldPosition, _transform.GetInvWorldMatrix(grid));
            foreach (var exclusion in site.UtilityExclusions)
                if (exclusion.Contains(local)) return false;
        }
        return true;
    }

    private void OfferRelays(Entity<CMUFobSiteComponent> site, Entity<CMUFobNetworkComponent> network, CMUFobMapComponent map)
    {
        if (network.Comp.State == CMUFobReserveState.Closed || network.Comp.JobExpiresAt != null
            || _timing.CurTime < network.Comp.NextJobAt || _timing.CurTime < site.Comp.NextOfferAttempt)
            return;
        var elapsed = _ticker.RoundDuration();
        var firstTier = elapsed < map.FarAfter ? CMUFobRelayTier.Near : CMUFobRelayTier.Intermediate;
        var secondTier = elapsed < map.IntermediateAfter ? CMUFobRelayTier.Near
            : elapsed < map.FarAfter ? CMUFobRelayTier.Intermediate : CMUFobRelayTier.Far;
        var pairs = new List<(EntityUid First, EntityUid Second)>();
        var available = new List<(CMUFobRelayRoute Route, EntityUid Relay)>();
        var rejected = new List<string>();
        foreach (var route in site.Comp.Relays)
        {
            if (route.Tier != firstTier && route.Tier != secondTier) continue;
            if (Available(route, network.Comp.Map, out var relay, out var reason))
                available.Add((route, relay));
            else
                rejected.Add($"{ToPrettyString(route.Anchor)} ({route.Tier}/{route.Approach}): {reason}");
        }
        foreach (var a in available)
        foreach (var b in available)
        {
            if (a.Route.Tier != firstTier || b.Route.Tier != secondTier || a.Route.Anchor == b.Route.Anchor
                || a.Route.Approach == b.Route.Approach)
                continue;
            pairs.Add((a.Relay, b.Relay));
        }
        _random.Shuffle(pairs);
        foreach (var (first, second) in pairs)
        {
            if (_reserve.TryOfferRelays(network, first, second, out _))
            {
                site.Comp.NoRelayPair = false;
                return;
            }
        }
        site.Comp.NoRelayPair = true;
        site.Comp.NextOfferAttempt = _timing.CurTime + TimeSpan.FromSeconds(30);
        Log.Warning($"FOB {site.Comp.Id}: no {firstTier}/{secondTier} pair with distinct approaches; " +
                    $"{available.Count} eligible endpoints, {pairs.Count} candidate pairs; retrying in 30s without charge. " +
                    $"Rejected: {string.Join("; ", rejected)}");
    }

    public override void Update(float frameTime)
    {
        if (!_enabled || !_reserve.Enabled || _timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        foreach (var uid in _pendingMaps)
        {
            if (TryComp<CMUFobMapComponent>(uid, out var map))
                TryPrepare((uid, map));
        }
        _pendingMaps.RemoveWhere(uid => !TryComp<CMUFobMapComponent>(uid, out var map) || map.Prepared || map.Rejected);
        var sites = EntityQueryEnumerator<CMUFobSiteComponent>();
        while (sites.MoveNext(out var uid, out var site))
        {
            if (site.Network is not { } bank || !TryComp<CMUFobNetworkComponent>(bank, out var network)
                || MetaData(uid).EntityPaused || MetaData(bank).EntityPaused
                || Transform(uid).GridUid is not { } grid || !TryComp<CMUFobMapComponent>(grid, out var map) || !map.Prepared)
                continue;
            OfferRelays((uid, site), (bank, network), map);
        }
    }
}
