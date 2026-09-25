using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Components;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Dropship.MultiDeck;

/// <summary>
/// Keeps the physical decks of a dropship in one Z network through loading, flight,
/// rotation and landing. Existing world maps are reused; only absent levels are created.
/// </summary>
public sealed partial class MultiDeckDropshipSystem : EntitySystem
{
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _levels = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private PowerLoaderSystem _powerLoader = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private RMCPullingSystem _pulling = default!;

    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<MultiDeckDropshipComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MultiDeckDropshipComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<MultiDeckDropshipComponent, FTLStartedEvent>(OnFlightStarted);
        SubscribeLocalEvent<MultiDeckDropshipComponent, FTLCompletedEvent>(OnFlightCompleted);
        SubscribeLocalEvent<MultiDeckDropshipComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<MultiDeckDropshipComponent> ship, ref MapInitEvent args)
    {
        if (ship.Comp.Initialized || Transform(ship).MapUid is not { } map)
            return;

        if (ship.Comp.DeckPaths.ContainsKey(0))
        {
            Log.Error($"Multi-deck dropship {ToPrettyString(ship)} defines a secondary deck at offset zero.");
            return;
        }

        var primaryDeck = EnsureComp<DropshipDeckComponent>(ship);
        primaryDeck.Ship = ship;
        primaryDeck.Offset = 0;
        Dirty(ship, primaryDeck);

        var loaded = new List<EntityUid>();
        try
        {
            foreach (var (offset, path) in ship.Comp.DeckPaths)
            {
                if (!TryGetOrCreateLevel(map, offset, out var deckMap) ||
                    !_loader.TryLoadGrid(Comp<MapComponent>(deckMap).MapId, path, out var grid))
                    throw new InvalidOperationException($"Could not load dropship deck {offset}: {path}");

                loaded.Add(grid.Value);
                // Content automatically gives every new grid a shuttle controller.
                // Followers are moved only by this assembly, never by FTL/thrusters.
                RemComp<ShuttleComponent>(grid.Value);
                _physics.SetBodyType(grid.Value, BodyType.Kinematic);
                _physics.SetFixedRotation(grid.Value, true);
                var deck = EnsureComp<DropshipDeckComponent>(grid.Value);
                deck.Ship = ship;
                deck.Offset = offset;
                Dirty(grid.Value, deck);
                ship.Comp.Decks.Add(offset, grid.Value);
                _dropships.RegisterDeckAttachmentPoints(ship, grid.Value);
                var children = Transform(grid.Value).ChildEnumerator;
                while (children.MoveNext(out var child))
                {
                    if (TryComp<DropshipWeaponPointComponent>(child, out var point))
                        _powerLoader.SyncAppearance((child, point));
                }
            }

            ship.Comp.Initialized = true;
            Synchronize(ship);
        }
        catch (Exception exception)
        {
            foreach (var grid in loaded)
                QueueDel(grid);
            ship.Comp.Decks.Clear();
            Log.Error($"Could not initialize {ToPrettyString(ship)}: {exception}");
        }
    }

    private void OnMove(Entity<MultiDeckDropshipComponent> ship, ref MoveEvent args)
    {
        if (!ship.Comp.Synchronizing && ship.Comp.Initialized)
            _pending.Add(ship);
    }

    private void OnFlightStarted(Entity<MultiDeckDropshipComponent> ship, ref FTLStartedEvent args)
        => Synchronize(ship);

    private void OnFlightCompleted(Entity<MultiDeckDropshipComponent> ship, ref FTLCompletedEvent args)
        => Synchronize(ship);

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        var pending = new List<EntityUid>(_pending);
        _pending.Clear();
        foreach (var uid in pending)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<MultiDeckDropshipComponent>(uid, out var ship))
                Synchronize((uid, ship));
        }
    }

    /// <summary>Place every deck at the primary grid's world position and rotation.</summary>
    public bool Synchronize(Entity<MultiDeckDropshipComponent> ship)
    {
        if (!ship.Comp.Initialized || ship.Comp.Synchronizing || Transform(ship).MapUid is not { } map)
            return false;

        var destinations = new List<(int Offset, EntityUid Grid, EntityUid Map)>();
        foreach (var (offset, grid) in ship.Comp.Decks)
        {
            if (TerminatingOrDeleted(grid) || !TryGetOrCreateLevel(map, offset, out var deckMap))
                return false;
            // FTL creates empty follower maps. Give each the cabin's moving
            // background, without making it a second primary FTL destination.
            if (TryComp<FTLMapComponent>(map, out var ftlMap))
            {
                var parallax = EnsureComp<ParallaxComponent>(deckMap);
                var background = TryComp<ParallaxComponent>(map, out var primaryParallax)
                    ? primaryParallax.Parallax
                    : ftlMap.Parallax;
                var velocity = primaryParallax?.TravelVelocity ?? new Vector2(0, FTLMapComponent.TravelSpeed);
                if (parallax.Parallax != background || parallax.TravelVelocity != velocity)
                {
                    parallax.Parallax = background;
                    parallax.TravelVelocity = velocity;
                    Dirty(deckMap, parallax);
                }
            }
            destinations.Add((offset, grid, deckMap));
        }

        var position = _transform.GetWorldPosition(ship);
        var rotation = _transform.GetWorldRotation(ship);
        ship.Comp.Synchronizing = true;
        try
        {
            foreach (var (offset, grid, deckMap) in destinations)
            {
                var gridTransform = Transform(grid);
                var oldMap = gridTransform.MapUid;
                var groundOccupants = new List<(EntityUid Uid, Vector2 Position, Angle Rotation)>();
                if (oldMap != deckMap && oldMap != null && ship.Comp.ExteriorDecks.Contains(offset))
                {
                    // Invisible tiles anchor servicing equipment, but do not
                    // make the undercarriage a passenger deck. A person or item
                    // standing on one must stay at the departure site.
                    var children = gridTransform.ChildEnumerator;
                    while (children.MoveNext(out var child))
                    {
                        var childTransform = Transform(child);
                        if (!childTransform.Anchored && childTransform.GridTraversal)
                            groundOccupants.Add((child, _transform.GetWorldPosition(child), _transform.GetWorldRotation(child)));
                    }
                }

                // A ground occupant left behind cannot retain a physics joint to
                // equipment travelling with the deck onto another map.
                foreach (var occupant in groundOccupants)
                    _pulling.TryStopAllPullsFromAndOn(occupant.Uid);
                _transform.SetCoordinates((grid, gridTransform, MetaData(grid)), new EntityCoordinates(deckMap, position), rotation: rotation);
                // Move the deck first so ordinary grid traversal cannot attach
                // these entities straight back onto its old footprint.
                foreach (var (uid, oldPosition, oldRotation) in groundOccupants)
                    _transform.SetCoordinates((uid, Transform(uid), MetaData(uid)), new EntityCoordinates(oldMap!.Value, oldPosition), rotation: oldRotation);
            }
        }
        finally
        {
            ship.Comp.Synchronizing = false;
        }
        _pending.Remove(ship);
        return true;
    }

    /// <summary>
    /// Landing markers describe ground level. Move the primary deck above that level
    /// and prepare all occupied levels before the flight destination is committed.
    /// </summary>
    public bool TryGetLandingCoordinates(EntityUid uid, EntityCoordinates ground, out EntityCoordinates coordinates)
    {
        coordinates = ground;
        if (!TryComp<MultiDeckDropshipComponent>(uid, out var ship))
            return true;
        if (!ship.Initialized)
            return false;

        var groundMap = _transform.ToMapCoordinates(ground);
        if (!_map.TryGetMap(groundMap.MapId, out var map) ||
            !TryGetOrCreateLevel(map.Value, ship.LandingOffset, out var primary))
            return false;

        foreach (var (offset, grid) in ship.Decks)
        {
            if (TerminatingOrDeleted(grid) || !TryGetOrCreateLevel(primary, offset, out _))
                return false;
        }
        coordinates = new EntityCoordinates(primary, groundMap.Position);
        return true;
    }

    public EntityCoordinates GetGroundCoordinates(EntityUid ship)
    {
        var xform = Transform(ship);
        if (TryComp<MultiDeckDropshipComponent>(ship, out var assembly) &&
            xform.MapUid is { } map && _levels.TryGetZNetwork(map, out var network))
        {
            var depth = (long) Comp<CMUZLevelMapComponent>(map).Depth - assembly.LandingOffset;
            if (depth is >= int.MinValue and <= int.MaxValue &&
                _levels.TryGetMapAtDepth(network.Value, (int) depth, out var ground))
                return new EntityCoordinates(ground, _transform.GetWorldPosition(ship));
        }
        return new EntityCoordinates(ship, Vector2.Zero);
    }

    public EntityUid GetGroundMap(EntityUid ship, EntityUid primaryMap)
    {
        if (TryComp<MultiDeckDropshipComponent>(ship, out var assembly) &&
            _levels.TryMapOffset(primaryMap, -assembly.LandingOffset, out var ground))
            return ground.Value;
        return primaryMap;
    }

    private bool TryGetOrCreateLevel(EntityUid map, int offset, out EntityUid target)
    {
        target = map;
        if (offset == 0)
            return true;
        if (!HasComp<MapComponent>(map) || TerminatingOrDeleted(map))
            return false;

        if (!_levels.TryGetZNetwork(map, out var network))
        {
            network = _levels.CreateZNetwork();
            if (!_levels.TryAddMapsIntoZNetwork(network.Value, new() { [map] = 0 }))
            {
                QueueDel(network.Value);
                return false;
            }
        }

        var depth = (long) Comp<CMUZLevelMapComponent>(map).Depth + offset;
        if (depth < int.MinValue || depth > int.MaxValue)
            return false;
        if (_levels.TryGetMapAtDepth(network.Value, (int) depth, out target))
            return true;

        target = _map.CreateMap();
        if (_levels.TryAddMapsIntoZNetwork(network.Value, new() { [target] = (int) depth }))
            return true;
        QueueDel(target);
        return false;
    }

    /// <summary>Strip a hijacked ship to its cabin without deleting loose ground occupants.</summary>
    public void RemoveSecondaryDecks(EntityUid ship)
    {
        if (!TryComp<MultiDeckDropshipComponent>(ship, out var assembly))
            return;

        foreach (var grid in assembly.Decks.Values)
        {
            _dropships.RemoveDeckAttachmentPoints(ship, grid);
            if (TerminatingOrDeleted(grid) || Transform(grid).MapUid is not { } map)
                continue;

            var occupants = new List<EntityUid>();
            var children = Transform(grid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                var transform = Transform(child);
                if (!transform.Anchored && transform.GridTraversal)
                    occupants.Add(child);
            }

            foreach (var occupant in occupants)
            {
                var rotation = _transform.GetWorldRotation(occupant);
                _transform.SetCoordinates(occupant, new EntityCoordinates(map, _transform.GetWorldPosition(occupant)));
                _transform.SetWorldRotation(occupant, rotation);
            }
        }

        // Component shutdown owns deletion of the followers. Removing the
        // assembly also removes the cabin's normal one-level landing offset.
        RemComp<MultiDeckDropshipComponent>(ship);
        RemComp<DropshipDeckComponent>(ship);
    }

    private void OnShutdown(Entity<MultiDeckDropshipComponent> ship, ref ComponentShutdown args)
    {
        _pending.Remove(ship);
        foreach (var (_, grid) in ship.Comp.Decks)
        {
            if (!TerminatingOrDeleted(grid))
                QueueDel(grid);
        }
        ship.Comp.Decks.Clear();
        ship.Comp.Initialized = false;
    }
}
