using System.Linq;
using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Robust.Shared.Map.Components;
using static Content.Server.Explosion.Components.ExplosionAirtightGridComponent;
using static Content.Server.Explosion.EntitySystems.ExplosionSystem;

namespace Content.Server.Explosion.EntitySystems;

/// <summary>
///     See <see cref="ExplosionTileFlood"/>. Each instance of this class corresponds to a seperate grid.
/// </summary>
public sealed class ExplosionGridTileFlood : ExplosionTileFlood
{
    public Entity<MapGridComponent> Grid;
    private bool _needToTransform = false;

    private Matrix3x2 _matrix = Matrix3x2.Identity;
    private Vector2 _offset;

    // Tiles which neighbor an exploding tile, but have not yet had the explosion spread to them due to an
    // airtight entity on the exploding tile that prevents the explosion from spreading in that direction. These
    // will be added as a neighbor after some delay, once the explosion on that tile is sufficiently strong to
    // destroy the airtight entity.
    private Dictionary<int, List<(Vector2i, AtmosDirection)>> _delayedNeighbors = new();

    private readonly IReadOnlyDictionary<Vector2i, TileData> _airtightMap;

    private float _maxIntensity;
    private float _intensityStepSize;
    private int _typeIndex;

    private UniqueVector2iSet _spaceTiles = new();
    private UniqueVector2iSet _processedSpaceTiles = new();

    public HashSet<Vector2i> SpaceJump = new();

    private readonly IReadOnlyDictionary<Vector2i, NeighborFlag> _edgeTiles;

    public ExplosionGridTileFlood(
        ExplosionGridSnapshot geometry,
        float maxIntensity,
        float intensityStepSize,
        int typeIndex,
        EntityUid? referenceGrid,
        Matrix3x2 spaceMatrix,
        Angle spaceAngle)
    {
        Grid = geometry.Grid;
        _airtightMap = geometry.Airtight;
        _maxIntensity = maxIntensity;
        _intensityStepSize = intensityStepSize;
        _typeIndex = typeIndex;
        _edgeTiles = geometry.Edges;

        if (referenceGrid == Grid.Owner)
            return;

        _needToTransform = true;
        var size = (float)geometry.TileSize;

        _matrix.M31 = size / 2;
        _matrix.M32 = size / 2;
        Matrix3x2.Invert(spaceMatrix, out var invSpace);
        var relativeAngle = geometry.Angle - spaceAngle;
        _matrix *= geometry.Matrix * invSpace;
        _offset = relativeAngle.RotateVec(new Vector2(size / 4, size / 4));
    }

    public IEnumerable<bool> PrepareSpaceTiles()
    {
        // initialise SpaceTiles
        foreach (var (tile, spaceNeighbors) in _edgeTiles)
        {
            yield return true;
            for (var i = 0; i < NeighbourVectors.Length; i++)
            {
                var dir = (NeighborFlag) (1 << i);
                if ((spaceNeighbors & dir) != NeighborFlag.Invalid)
                    _spaceTiles.Add(tile + NeighbourVectors[i]);
            }
        }

    }

    public override void InitTile(Vector2i initialTile)
    {
        TileLists[0] = new() { initialTile };

        if (_airtightMap.ContainsKey(initialTile))
            EnteredBlockedTiles.Add(initialTile);
        else
            ProcessedTiles.Add(initialTile);
    }

    public IEnumerable<bool> PrepareNewTiles(int iteration, HashSet<Vector2i>? gridJump)
    {
        SpaceJump = new();
        NewTiles = new();
        NewBlockedTiles = new();

        // Mark tiles as entered if any were just freed due to airtight/explosion blockers being destroyed.
        if (FreedTileLists.TryGetValue(iteration, out var freed))
        {
            HashSet<Vector2i> toRemove = new();
            foreach (var tile in freed)
            {
                if (!EnteredBlockedTiles.Add(tile))
                    toRemove.Add(tile);
            }

            freed.ExceptWith(toRemove);
            NewFreedTiles = freed;
        }
        else
        {
            NewFreedTiles = new();
            FreedTileLists[iteration] = NewFreedTiles;
        }

        // Add adjacent tiles
        if (TileLists.TryGetValue(iteration - 2, out var adjacent))
            foreach (var step in AddNewAdjacentTiles(iteration, adjacent, false)) yield return step;
        if (FreedTileLists.TryGetValue(iteration - 2, out var delayedAdjacent))
            foreach (var step in AddNewAdjacentTiles(iteration, delayedAdjacent, true)) yield return step;

        // Add diagonal tiles
        if (TileLists.TryGetValue(iteration - 3, out var diagonal))
            foreach (var step in AddNewDiagonalTiles(iteration, diagonal, false)) yield return step;
        if (FreedTileLists.TryGetValue(iteration - 3, out var delayedDiagonal))
            foreach (var step in AddNewDiagonalTiles(iteration, delayedDiagonal, true)) yield return step;

        // Add delayed tiles
        foreach (var step in AddDelayedNeighbors(iteration)) yield return step;

        // Tiles from Spaaaace
        if (gridJump != null)
        {
            foreach (var tile in gridJump)
            {
                yield return true;
                ProcessNewTile(iteration, tile, AtmosDirection.Invalid);
            }
        }

        // Store new tiles
        if (NewTiles.Count != 0)
            TileLists[iteration] = NewTiles;
        if (NewBlockedTiles.Count != 0)
            BlockedTileLists[iteration] = NewBlockedTiles;

        yield break;
    }

    protected override void ProcessNewTile(int iteration, Vector2i tile, AtmosDirection entryDirections)
    {
        if (Grid.Comp.Deleted) return;
        // Is there an airtight blocker on this tile?
        if (!_airtightMap.TryGetValue(tile, out var tileData))
        {
            // No blocker. Ezy. Though maybe this a space tile?

            if (_spaceTiles.Contains(tile))
                JumpToSpace(tile);
            else if (ProcessedTiles.Add(tile))
                NewTiles.Add(tile);

            return;
        }

        // If the explosion is entering this new tile from an unblocked direction, we add it directly. Note that because
        // for space -> grid jumps, we don't have a direction from which the explosion came, we will only assume it is
        // unblocked if all space-facing directions are unblocked. Though this could eventually be done properly.

        bool blocked;
        var blockedDirections = tileData.BlockedDirections;
        if (entryDirections == AtmosDirection.Invalid) // is coming from space?
        {
            blocked = AnyNeighborBlocked(_edgeTiles.GetValueOrDefault(tile), blockedDirections); // at least one space direction is blocked.
        }
        else
            blocked = (blockedDirections & entryDirections) == entryDirections;// **ALL** entry directions are blocked

        if (blocked)
        {
            // was this tile already entered from some other direction?
            if (EnteredBlockedTiles.Contains(tile))
                return;

            // Did the explosion already attempt to enter this tile from some other direction?
            if (!UnenteredBlockedTiles.Add(tile))
                return;

            NewBlockedTiles.Add(tile);

            // At what explosion iteration would this blocker be destroyed?
            var required = tileData.Tolerances.Values[_typeIndex];
            if (required > _maxIntensity)
                return; // blocker is never destroyed.

            var clearIteration = iteration + (int) MathF.Ceiling((float)required / _intensityStepSize);
            if (FreedTileLists.TryGetValue(clearIteration, out var list))
                list.Add(tile);
            else
                FreedTileLists[clearIteration] = new() { tile };

            return;
        }

        // was this tile already entered from some other direction?
        if (!EnteredBlockedTiles.Add(tile))
            return;

        // Did the explosion already attempt to enter this tile from some other direction?
        if (UnenteredBlockedTiles.Contains(tile))
        {
            NewFreedTiles.Add(tile);
            return;
        }

        // This is a completely new tile, and we just so happened to enter it from an unblocked direction.
        NewTiles.Add(tile);
    }

    private void JumpToSpace(Vector2i tile)
    {
        // Did we already jump/process this tile?
        if (!_processedSpaceTiles.Add(tile))
            return;

        if (!_needToTransform)
        {
            SpaceJump.Add(tile);
            return;
        }

        var center = Vector2.Transform(tile, _matrix);
        SpaceJump.Add(new((int) MathF.Floor(center.X + _offset.X), (int) MathF.Floor(center.Y + _offset.Y)));
        SpaceJump.Add(new((int) MathF.Floor(center.X - _offset.Y), (int) MathF.Floor(center.Y + _offset.X)));
        SpaceJump.Add(new((int) MathF.Floor(center.X - _offset.X), (int) MathF.Floor(center.Y - _offset.Y)));
        SpaceJump.Add(new((int) MathF.Floor(center.X + _offset.Y), (int) MathF.Floor(center.Y - _offset.X)));
    }

    private IEnumerable<bool> AddDelayedNeighbors(int iteration)
    {
        if (!_delayedNeighbors.TryGetValue(iteration, out var delayed))
            yield break;

        foreach (var (tile, direction) in delayed)
        {
            yield return true;
            ProcessNewTile(iteration, tile, direction);
        }

        _delayedNeighbors.Remove(iteration);
    }

    // Gets the tiles that are directly adjacent to other tiles. If a currently exploding tile has an airtight entity
    // that blocks the explosion from propagating in some direction, those tiles are added to a list of delayed tiles
    // that will be added to the explosion in some future iteration.
    private IEnumerable<bool> AddNewAdjacentTiles(int iteration, IEnumerable<Vector2i> tiles, bool ignoreTileBlockers = false)
    {
        foreach (var tile in tiles)
        {
            yield return true;
            if (Grid.Comp.Deleted) yield break;
            var blockedDirections = AtmosDirection.Invalid;
            FixedPoint2 sealIntegrity = 0;

            // Note that if (grid, tile) is not a valid key, then airtight.BlockedDirections will default to 0 (no blocked directions)
            if (_airtightMap.TryGetValue(tile, out var tileData))
            {
                blockedDirections = tileData.BlockedDirections;
                sealIntegrity = tileData.Tolerances.Values[_typeIndex];
            }

            // First, yield any neighboring tiles that are not blocked by airtight entities on this tile
            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var direction = (AtmosDirection) (1 << i);
                if (ignoreTileBlockers || !blockedDirections.IsFlagSet(direction))
                {
                    ProcessNewTile(iteration, tile.Offset(direction), i.ToOppositeDir());
                }
            }

            // If there are no blocked directions, we are done with this tile.
            if (ignoreTileBlockers || blockedDirections == AtmosDirection.Invalid)
                continue;

            // This tile has one or more airtight entities anchored to it blocking the explosion from traveling in
            // some directions. First, check whether this blocker can even be destroyed by this explosion?
            if (sealIntegrity > _maxIntensity)
                continue;

            // At what explosion iteration would this blocker be destroyed?
            var clearIteration = iteration + (int) MathF.Ceiling((float) sealIntegrity / _intensityStepSize);

            // Get the delayed neighbours list
            if (!_delayedNeighbors.TryGetValue(clearIteration, out var list))
            {
                list = new();
                _delayedNeighbors[clearIteration] = list;
            }

            // Check which directions are blocked, and add them to the list.
            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var direction = (AtmosDirection) (1 << i);
                if (blockedDirections.IsFlagSet(direction))
                {
                    list.Add((tile.Offset(direction), i.ToOppositeDir()));
                }
            }
        }
    }

    protected override AtmosDirection GetUnblockedDirectionOrAll(Vector2i tile)
    {
        return ~_airtightMap.GetValueOrDefault(tile).BlockedDirections;
    }
}
