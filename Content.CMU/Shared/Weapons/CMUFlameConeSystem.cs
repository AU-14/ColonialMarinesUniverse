using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Line;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Weapons;

/// <summary>
/// FLOW-22: a flamethrower projects its flame as an arrow-shaped cone along the whole centre line from
/// <see cref="LineSystem.DrawLine"/>. Reach comes from the fire spec (<see cref="CMUFuelProfileSystem.NonSpecialistReach"/>
/// already shortens it for non-specialist tanks); this only decides how wide each row is. Row widths follow
/// <see cref="SpreadWidths"/> for spreading fuels (B-gel: 2 -> 3 -> 3 -> 4 -> 5) and <see cref="NormalWidths"/> otherwise
/// (UT: 1 -> 2 -> 3 -> 3 -> 4), capped at <see cref="SpecialistMaxWidth"/> for specialist tanks and
/// <see cref="StandardMaxWidth"/> for every other tank. Each added tile must be reachable from the muzzle
/// (<see cref="LineSystem.CanReachTile"/>), so the cone never wraps round walls or doors.
/// </summary>
public sealed partial class CMUFlameConeSystem : EntitySystem
{
    [Dependency] private LineSystem _line = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Row widths of a spreading fuel's flame; rows past the end keep the last width.</summary>
    public static readonly int[] SpreadWidths = { 2, 3, 3, 4, 5 };

    /// <summary>Row widths of an ordinary fuel's flame; rows past the end keep the last width.</summary>
    public static readonly int[] NormalWidths = { 1, 2, 3, 3, 4 };

    public const int StandardMaxWidth = 3;
    public const int SpecialistMaxWidth = 5;

    /// <summary>Width in tiles of row <paramref name="row"/> (1-based).</summary>
    public static int RowWidth(int row, bool fireSpread, bool specialist)
    {
        var widths = fireSpread ? SpreadWidths : NormalWidths;
        var width = widths[Math.Clamp(row, 1, widths.Length) - 1];
        return Math.Min(width, specialist ? SpecialistMaxWidth : StandardMaxWidth);
    }

    /// <summary>
    /// The centre <paramref name="line"/> widened into an arrow, ordered by when each tile ignites. Side tiles ignite
    /// together with their row's centre tile.
    /// </summary>
    public List<LineTile> WidenToCone(EntityCoordinates start, List<LineTile> line, bool fireSpread, bool specialist)
    {
        if (line.Count == 0)
            return line;

        var origin = start.SnapToGrid(EntityManager);
        var frame = origin.EntityId;
        var centers = new List<(Vector2 Position, TimeSpan At)>(line.Count);
        var seen = new HashSet<Vector2>();
        var result = new List<LineTile>(line.Count * SpecialistMaxWidth);
        foreach (var tile in line)
        {
            var local = _transform.ToCoordinates(frame, tile.Coordinates).SnapToGrid(EntityManager);
            centers.Add((local.Position, tile.At));
            seen.Add(tile.Coordinates.Position);
            result.Add(tile);
        }

        var axis = centers[^1].Position - origin.Position;
        if (axis.LengthSquared() < 0.01f)
            return result;

        // Grid step perpendicular to the flame: (±1, 0)/(0, ±1) when firing straight, a diagonal step otherwise.
        var dir = Vector2.Normalize(axis);
        var side = new Vector2(MathF.Round(-dir.Y), MathF.Round(dir.X));
        if (side.LengthSquared() < 0.01f)
            return result;

        for (var row = 1; row <= centers.Count; row++)
        {
            var (center, at) = centers[row - 1];
            var width = RowWidth(row, fireSpread, specialist);
            // Alternate sides outward from the centre: 2 adds one side, 3 both, 4 one more, 5 both again.
            for (var i = 1; i < width; i++)
            {
                var offset = (i + 1) / 2 * (i % 2 == 1 ? 1 : -1);
                var coordinates = new EntityCoordinates(frame, center + side * offset);
                var mapCoordinates = _transform.ToMapCoordinates(coordinates);
                if (!seen.Add(mapCoordinates.Position))
                    continue;

                if (!_line.CanReachTile(start, coordinates, hitBlocker: true))
                    continue;

                result.Add(new LineTile(mapCoordinates, at));
            }
        }

        // Stable, so each centre tile stays ahead of the side tiles it lights.
        return result.OrderBy(t => t.At).ToList();
    }
}
