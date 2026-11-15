// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Farming;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Farming;

// Spawns an entity every so often next to a spawner (for example fruit on a plant), but not where enough of them are already near.
public sealed class PeriodicSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MetaDataComponent>> _near = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PeriodicSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PeriodicSpawnerComponent> spawner, ref MapInitEvent args)
    {
        spawner.Comp.NextAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(spawner.Comp.Interval));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PeriodicSpawnerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawner, out var xform))
        {
            if (_timing.CurTime < spawner.NextAt)
                continue;

            spawner.NextAt = _timing.CurTime + TimeSpan.FromSeconds(spawner.Interval * _random.NextFloat(0.75f, 1.25f));
            TrySpawn((uid, spawner), xform);
        }
    }

    // One thing, if the place is not full: it is counted by the same things that it makes.
    public bool TrySpawn(Entity<PeriodicSpawnerComponent> spawner, TransformComponent xform)
    {
        var comp = spawner.Comp;
        if (comp.Prototypes.Count == 0 || !_random.Prob(comp.Chance))
            return false;

        _near.Clear();
        _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(xform.Coordinates), comp.Radius, _near);
        var count = 0;
        foreach (var other in _near)
        {
            if (other.Comp.EntityPrototype is { } proto && comp.Prototypes.Contains(proto.ID))
                count++;
        }

        if (count >= comp.MaxNearby)
            return false;

        var offset = _random.NextVector2(comp.Radius);
        var coords = xform.Coordinates.Offset(offset);
        if (_transform.GetGrid(coords) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp) || _map.GetTileRef(grid, gridComp, coords).Tile.IsEmpty)
            return false;

        Spawn(_random.Pick(comp.Prototypes), coords);
        return true;
    }
}
