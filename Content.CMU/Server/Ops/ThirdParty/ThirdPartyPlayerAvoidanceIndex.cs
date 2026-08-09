using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Ops.ThirdParty;

/// <summary>
/// One-shot spatial index of living player positions used while choosing third-party spawn markers.
/// </summary>
internal sealed class ThirdPartyPlayerAvoidanceIndex
{
    internal const float Radius = 8f;

    private readonly Dictionary<PlayerCell, List<Vector2>> _positions = [];

    public void Add(MapId map, Vector2 worldPosition)
    {
        var cell = GetCell(map, worldPosition);
        if (!_positions.TryGetValue(cell, out var positions))
        {
            positions = [];
            _positions.Add(cell, positions);
        }

        positions.Add(worldPosition);
    }

    public bool IsBlocked(MapId map, Vector2 worldPosition)
    {
        var center = GetCell(map, worldPosition);
        var radiusSquared = Radius * Radius;
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            for (var yOffset = -1; yOffset <= 1; yOffset++)
            {
                var cell = new PlayerCell(map, center.X + xOffset, center.Y + yOffset);
                if (!_positions.TryGetValue(cell, out var positions))
                    continue;

                foreach (var playerPosition in positions)
                {
                    if (Vector2.DistanceSquared(worldPosition, playerPosition) <= radiusSquared)
                        return true;
                }
            }
        }

        return false;
    }

    private static PlayerCell GetCell(MapId map, Vector2 position)
    {
        return new PlayerCell(
            map,
            (int)MathF.Floor(position.X / Radius),
            (int)MathF.Floor(position.Y / Radius));
    }

    private readonly record struct PlayerCell(MapId Map, int X, int Y);
}
