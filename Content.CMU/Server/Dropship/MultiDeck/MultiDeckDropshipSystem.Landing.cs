using System.Numerics;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Dropship.MultiDeck;

public sealed partial class MultiDeckDropshipSystem
{
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    /// <summary>
    /// Markers occupy tile centers, while a grid's origin is a tile corner. Keep
    /// multi-deck landings aligned with the pad so the ramp joins its stairs cleanly.
    /// Tactical destinations that already name a tile corner remain unchanged.
    /// </summary>
    public EntityCoordinates GetLandingOrigin(EntityUid ship, EntityCoordinates coordinates, EntityUid? destination = null)
    {
        if (!HasComp<MultiDeckDropshipComponent>(ship))
            return coordinates;

        coordinates = _transform.GetMoverCoordinates(coordinates);
        if (TryComp<DropshipDestinationComponent>(destination, out var pad))
            coordinates = coordinates.Offset(pad.MultiDeckOffset);
        return new EntityCoordinates(coordinates.EntityId,
            new Vector2(MathF.Floor(coordinates.X), MathF.Floor(coordinates.Y)));
    }

    /// <summary>
    /// Checks the cabin and decks below it without creating maps. Upper artwork may
    /// overlap a carrier's roof. Mohawks bypass all clearance checks.
    /// </summary>
    public bool IsLandingClear(EntityUid ship, EntityCoordinates ground, Angle rotation,
        ISet<Vector2i>? blocked = null)
    {
        // Mohawks accept terrain, occupied footprints and other landing reservations.
        if (HasComp<MohawkMechanismsComponent>(ship))
            return true;

        var coordinates = _transform.ToMapCoordinates(ground);
        if (OverlapsReservedLanding(ship, coordinates, rotation))
            return false;
        if (!TryComp<MultiDeckDropshipComponent>(ship, out var assembly))
            return true;
        if (!assembly.Initialized)
            return false;

        if (!_map.TryGetMap(coordinates.MapId, out var groundMap))
            return false;

        var clear = CheckDeck(ship, assembly.LandingOffset);
        foreach (var (offset, grid) in assembly.Decks)
        {
            if (offset < 0)
                clear &= CheckDeck(grid, assembly.LandingOffset + offset);
        }
        return clear;

        bool CheckDeck(EntityUid grid, int offset)
        {
            if (!TryComp<MapGridComponent>(grid, out var deck))
                return false;

            var target = groundMap.Value;
            if (offset != 0)
            {
                // Absent levels are empty space. They are created only when flight commits.
                if (!_levels.TryMapOffset(target, offset, out var level))
                    return true;
                target = level.Value;
            }

            var bounds = new Box2Rotated(deck.LocalAABB.Translated(coordinates.Position), rotation, coordinates.Position);
            var terrain = new List<Entity<MapGridComponent>>();
            _map.FindGridsIntersecting(Comp<MapComponent>(target).MapId, bounds, ref terrain);
            var result = true;
            foreach (var tile in _map.GetAllTiles(grid, deck))
            {
                var rotated = rotation.RotateVec((tile.GridIndices + new Vector2(0.5f)) * deck.TileSize);
                var point = new EntityCoordinates(target, coordinates.Position + rotated);
                foreach (var other in terrain)
                {
                    if (other.Owner == ship || assembly.Decks.ContainsValue(other.Owner))
                        continue;
                    var indices = _map.CoordinatesToTile(other, other.Comp, point);
                    var destination = _map.GetTileRef(other, other.Comp, indices);
                    const CollisionGroup mask = CollisionGroup.Impassable | CollisionGroup.LowImpassable |
                                                CollisionGroup.MidImpassable | CollisionGroup.HighImpassable;
                    if ((offset > 0 && !CMUZLevelOpeningCache.IsOpeningTile(destination.Tile, _tileDefinitions)) ||
                        _turf.IsTileBlocked(destination, mask, 0.001f))
                    {
                        result = false;
                        blocked?.Add(new Vector2i((int) MathF.Floor(rotated.X), (int) MathF.Floor(rotated.Y)));
                        if (blocked == null)
                            return false;
                        break;
                    }
                }
            }
            return result;
        }
    }

    private bool OverlapsReservedLanding(EntityUid ship, MapCoordinates ground, Angle rotation)
    {
        var destinations = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (destinations.MoveNext(out var destination, out var reservation, out var destinationTransform))
        {
            if (reservation.Ship is not { } other || other == ship || TerminatingOrDeleted(other) ||
                (!HasComp<MultiDeckDropshipComponent>(ship) && !HasComp<MultiDeckDropshipComponent>(other)))
                continue;
            var position = _transform.ToMapCoordinates(GetLandingOrigin(other, destinationTransform.Coordinates, destination));
            if (position.MapId != ground.MapId)
                continue;

            // Reserve the whole hull envelope while a ship is inbound. Its grids
            // are still in transit, so a check of the destination terrain cannot
            // detect an overlap with an adjacent reserved pad yet.
            var otherRotation = _transform.GetWorldRotation(destination);
            foreach (var grid in LandingGrids(ship))
            {
                var bounds = new Box2Rotated(grid.Comp.LocalAABB.Translated(ground.Position), rotation, ground.Position).CalcBoundingBox();
                foreach (var otherGrid in LandingGrids(other))
                {
                    var otherBounds = new Box2Rotated(otherGrid.Comp.LocalAABB.Translated(position.Position), otherRotation, position.Position).CalcBoundingBox();
                    // Adjacent pads may have envelopes whose edges touch. Only
                    // shared area occupies the other ship's reserved volume.
                    if (bounds.Left < otherBounds.Right && bounds.Right > otherBounds.Left &&
                        bounds.Bottom < otherBounds.Top && bounds.Top > otherBounds.Bottom)
                        return true;
                }
            }
        }
        return false;
    }

    private IEnumerable<Entity<MapGridComponent>> LandingGrids(EntityUid ship)
    {
        if (TryComp<MapGridComponent>(ship, out var primary))
            yield return (ship, primary);
        if (!TryComp<MultiDeckDropshipComponent>(ship, out var assembly))
            yield break;
        foreach (var (offset, deck) in assembly.Decks)
        {
            if (offset < 0 && TryComp<MapGridComponent>(deck, out var grid))
                yield return (deck, grid);
        }
    }
}
