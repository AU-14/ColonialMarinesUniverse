using System.Linq;
using Content.Shared._RMC14.Areas;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Intel;

public sealed partial class IntelSystem
{
    [Dependency] private SharedMapSystem _cmuMap = default!;

    private const string NoArea = "(no area)";

    private readonly Dictionary<EntityUid, string> _cmuSpawnerAreas = new();

    private readonly Dictionary<string, int> _cmuAreaCounts = new();

    private readonly Dictionary<EntityUid, int> _cmuSpawnerUses = new();

    private void ResetCMUSpread()
    {
        _cmuSpawnerAreas.Clear();
        _cmuAreaCounts.Clear();
        _cmuSpawnerUses.Clear();
    }

    private Entity<IntelSpawnerComponent> PickSpreadSpawner(List<Entity<IntelSpawnerComponent>> spawners)
    {
        var bestAreaCount = int.MaxValue;
        var bestUses = int.MaxValue;
        var candidates = new List<Entity<IntelSpawnerComponent>>();

        foreach (var spawner in spawners)
        {
            var area = GetSpawnerArea(spawner);
            var areaCount = _cmuAreaCounts.GetValueOrDefault(area);
            var uses = _cmuSpawnerUses.GetValueOrDefault(spawner.Owner);

            if (areaCount > bestAreaCount || areaCount == bestAreaCount && uses > bestUses)
                continue;

            if (areaCount < bestAreaCount || uses < bestUses)
            {
                bestAreaCount = areaCount;
                bestUses = uses;
                candidates.Clear();
            }

            candidates.Add(spawner);
        }

        var picked = _random.Pick(candidates);
        var pickedArea = GetSpawnerArea(picked);
        _cmuAreaCounts[pickedArea] = _cmuAreaCounts.GetValueOrDefault(pickedArea) + 1;
        _cmuSpawnerUses[picked.Owner] = _cmuSpawnerUses.GetValueOrDefault(picked.Owner) + 1;
        return picked;
    }

    private string GetSpawnerArea(EntityUid spawner)
    {
        if (_cmuSpawnerAreas.TryGetValue(spawner, out var cached))
            return cached;

        var area = NoArea;
        var coords = _transform.GetMoverCoordinates(spawner);
        if (_transform.GetGrid(coords) is { } gridUid &&
            TryComp(gridUid, out MapGridComponent? grid) &&
            TryComp(gridUid, out AreaGridComponent? areaGrid))
        {
            var tile = _cmuMap.CoordinatesToTile(gridUid, grid, coords);
            var areas = areaGrid.Areas;
            if (areas.TryGetValue(tile, out var areaProto))
                area = areaProto.Id;
        }

        _cmuSpawnerAreas[spawner] = area;
        return area;
    }

    private void LogCMUSpread()
    {
        var total = _cmuAreaCounts.Values.Sum();
        var top = string.Join(", ", _cmuAreaCounts
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .Select(kv => $"{kv.Key}={kv.Value}"));
        Log.Info($"Spawned {total} intel across {_cmuAreaCounts.Count} areas from {_cmuSpawnerAreas.Count} spawners. Most: {top}");
    }
}
