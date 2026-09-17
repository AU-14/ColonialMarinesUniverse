using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Trigger;
using Content.Shared._RMC14.Atmos;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Atmos;

public sealed class CMUFireSuppressantOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFireSuppressantOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<CMUFireSuppressantOnTriggerComponent> ent, ref TriggerEvent args)
    {
        args.Handled = true;

        var coords = _transform.GetMoverCoordinates(ent);
        var radius = ent.Comp.Radius;

        foreach (var fire in _lookup.GetEntitiesInRange<TileFireComponent>(coords, radius))
        {
            ExtinguishHotspot(_transform.GetMoverCoordinates(fire));
            QueueDel(fire);
        }

        ExtinguishHotspot(coords);

        foreach (var mob in _lookup.GetEntitiesInRange<FlammableComponent>(coords, radius))
        {
            if (mob.Comp.OnFire)
                _rmcFlammable.Extinguish((mob.Owner, mob.Comp));
        }
    }

    private void ExtinguishHotspot(EntityCoordinates coords)
    {
        if (_transform.GetGrid(coords) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        _atmosphere.HotspotExtinguish(grid, _map.CoordinatesToTile(grid, gridComp, coords));
    }
}
