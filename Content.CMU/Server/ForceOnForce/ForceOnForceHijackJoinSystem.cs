using System.Linq;
using System.Numerics;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Rules;
using Content.Shared.Buckle;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.ForceOnForce;

/// <summary>One opt-in transport offer per player when an opposing dropship starts its hijack flight.</summary>
public sealed partial class ForceOnForceHijackJoinSystem : EntitySystem
{
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private ForceOnForceSystem _factions = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private RMCPlanetSystem _planet = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private RMCPullingSystem _pulling = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly Dictionary<ForceOnForceHijackJoinEui, Offer> _offers = new();

    private sealed record Offer(EntityUid User, EntityUid Dropship, EntityUid Destination,
        string Faction, bool Attacking, EntityCoordinates BoardingPoint, FTLComponent Flight);

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipComponent, ForceOnForceHijackStartedEvent>(OnHijackStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private bool IsForceOnForce =>
        _ticker.CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Both sides may join from the planet or the attackers' carrier, but not from the fight itself.</summary>
    public bool IsEligible(EntityUid user, EntityUid dropship, string attacker)
    {
        if (!IsForceOnForce || TerminatingOrDeleted(user) ||
            !TryComp<MobStateComponent>(user, out var mob) || mob.CurrentState == MobState.Dead ||
            _factions.GetFaction(user) is not { } faction ||
            (faction != attacker && faction != ForceOnForceSystem.Opponent(attacker)) ||
            _dropships.TryGetGridDropship(user, out var aboard) && aboard.Owner == dropship)
            return false;

        var carrier = _dropships.GetCarrierFaction(user);
        if (carrier != null)
            return string.Equals(carrier, attacker, StringComparison.OrdinalIgnoreCase);
        return _planet.IsOnPlanet(Transform(user)) ||
               _planet.TryGetPlanetSurfaceCoordinates(_transform.GetMapCoordinates(user), out _);
    }

    private void OnHijackStarted(Entity<DropshipComponent> ship, ref ForceOnForceHijackStartedEvent args)
    {
        if (!IsForceOnForce || !TryComp<FTLComponent>(ship, out var flight) ||
            _factions.GetFaction(args.Hijacker) is not { } attacker ||
            !string.Equals(ship.Comp.HijackerFaction, attacker, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_dropships.GetCarrierFaction(args.Destination), ForceOnForceSystem.Opponent(attacker),
                StringComparison.OrdinalIgnoreCase) ||
            !_dropships.TryGetGridDropship(args.Hijacker, out var aboard) || aboard.Owner != ship.Owner)
            return;

        // Keep the boarding point relative to its deck as the dropship enters hyperspace.
        var hijackerTransform = Transform(args.Hijacker);
        if (hijackerTransform.GridUid is not { } deck)
            return;
        var boarding = _transform.ToCoordinates(deck, _transform.GetMapCoordinates(args.Hijacker));
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } user || !IsEligible(user, ship, attacker))
                continue;
            foreach (var previous in _offers.Keys.Where(e => e.Player == session).ToArray())
                previous.Close();

            var faction = _factions.GetFaction(user)!;
            var attacking = faction == attacker;
            var ui = new ForceOnForceHijackJoinEui(this, attacking);
            _offers.Add(ui, new Offer(user, ship, args.Destination, faction, attacking, boarding, flight));
            _eui.OpenEui(ui, session);
        }
    }

    private bool IsValid(ForceOnForceHijackJoinEui ui, Offer offer)
    {
        return ui.Player.AttachedEntity == offer.User &&
               _factions.GetFaction(offer.User) == offer.Faction &&
               !TerminatingOrDeleted(offer.Dropship) && !TerminatingOrDeleted(offer.Destination) &&
               TryComp<FTLComponent>(offer.Dropship, out var flight) && flight == offer.Flight &&
               flight.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving &&
               IsEligible(offer.User, offer.Dropship,
                   offer.Attacking ? offer.Faction : ForceOnForceSystem.Opponent(offer.Faction)!);
    }

    public void Respond(ForceOnForceHijackJoinEui ui, bool join)
    {
        if (!_offers.Remove(ui, out var offer) || !join || !IsValid(ui, offer))
            return;

        if (!TryGetDestination(offer, out var coordinates) || _containers.IsEntityOrParentInContainer(offer.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-fof-hijack-join-unavailable"), offer.User, offer.User);
            return;
        }

        _buckle.Unbuckle((offer.User, null), offer.User);
        _pulling.TryStopAllPullsFromAndOn(offer.User);
        _transform.SetCoordinates(offer.User, coordinates);
        _transform.AttachToGridOrMap(offer.User);
    }

    private bool TryGetDestination(Offer offer, out EntityCoordinates coordinates)
    {
        if (offer.Attacking)
            return TryFindFloor(offer.BoardingPoint, out coordinates);

        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var points = new List<(EntityUid Uid, int Priority)>();
        while (query.MoveNext(out var uid, out var spawn, out _))
        {
            if (spawn.SpawnType is not (SpawnPointType.Job or SpawnPointType.LateJoin or
                SpawnPointType.LateJoinGovfor or SpawnPointType.LateJoinOpfor) ||
                !string.Equals(_dropships.GetCarrierFaction(uid), offer.Faction, StringComparison.OrdinalIgnoreCase) ||
                _dropships.TryGetGridDropship(uid, out _))
                continue;
            points.Add((uid, spawn.SpawnType == SpawnPointType.Job ? 1 : 0));
        }

        foreach (var (uid, _) in points.OrderBy(p => p.Priority))
        {
            if (TryFindFloor(Transform(uid).Coordinates, out coordinates))
                return true;
        }

        coordinates = default;
        return false;
    }

    private bool TryFindFloor(EntityCoordinates around, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (TerminatingOrDeleted(around.EntityId) || _transform.GetGrid(around) is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;
        var center = _maps.CoordinatesToTile(grid, gridComp, around);
        // Search from the anchor outwards, staying inside the same deck and avoiding walls/space.
        for (var radius = 0; radius <= 3; radius++)
        for (var x = -radius; x <= radius; x++)
        for (var y = -radius; y <= radius; y++)
        {
            if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                continue;
            var tile = _maps.GetTileRef(grid, gridComp, center + new Vector2i(x, y));
            if (_turf.IsSpace(tile.Tile) || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                continue;
            coordinates = new EntityCoordinates(grid, (tile.GridIndices + new Vector2(.5f)) * gridComp.TileSize);
            return true;
        }
        return false;
    }

    public void Forget(ForceOnForceHijackJoinEui ui) => _offers.Remove(ui);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_offers.Count == 0)
            return;
        foreach (var (ui, offer) in _offers.ToArray())
        {
            if (!IsValid(ui, offer))
                ui.Close();
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        foreach (var ui in _offers.Keys.ToArray())
            ui.Close();
    }
}
