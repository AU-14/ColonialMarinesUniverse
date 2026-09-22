using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Atmos;

public sealed class CMUWideAirtightSystem : EntitySystem
{
    [Dependency] private readonly AirtightSystem _airtight = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<EntityUid> _tileEnts = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUWideAirtightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUWideAirtightComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CMUWideAirtightComponent, ReAnchorEvent>(OnReAnchor);
        SubscribeLocalEvent<CMUWideAirtightComponent, AirtightChanged>(OnDoorAirtightChanged);
        SubscribeLocalEvent<CMUWideAirtightComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<CMUWideAirtightComponent> ent, ref MapInitEvent args)
        => Reseal(ent);

    // Map loading sets anchored without raising this, so MapInit covers placed doors;
    // every runtime transition (admin move, construction, deconstruction) lands here
    private void OnAnchorChanged(Entity<CMUWideAirtightComponent> ent, ref AnchorStateChangedEvent args)
        => Reseal(ent);

    // Grid merges move anchored entities between grids without an anchor state change;
    // AirtightSystem covers the same event for the door's own tile
    private void OnReAnchor(Entity<CMUWideAirtightComponent> ent, ref ReAnchorEvent args)
        => Reseal(ent);

    // Side tiles follow the anchor tile: whatever the door's own airtight does, the
    // seals do, so an open door breathes across its full width
    private void OnDoorAirtightChanged(Entity<CMUWideAirtightComponent> ent, ref AirtightChanged args)
        => SyncSeals(ent, args.Airtight.AirBlocked);

    private void Reseal(Entity<CMUWideAirtightComponent> ent)
    {
        foreach (var seal in ent.Comp.Seals)
            QueueDel(seal);
        ent.Comp.Seals.Clear();

        var xform = Transform(ent);
        if (!xform.Anchored
            || !TryComp(xform.GridUid, out MapGridComponent? grid))
            return;

        var tile = _transform.GetGridTilePositionOrDefault((ent, xform), grid);

        foreach (var offset in ent.Comp.Offsets)
        {
            var rotated = xform.LocalRotation.RotateVec(offset);
            var offsetTile = new Vector2i((int) MathF.Round(rotated.X), (int) MathF.Round(rotated.Y));
            var targetTile = tile + offsetTile;

            // Map saves bake spawned seals into the YAML; without this every save
            // cycle would stack a fresh set on the old ones
            _map.GetAnchoredEntities((xform.GridUid.Value, grid), targetTile, _tileEnts);
            foreach (var baked in _tileEnts)
            {
                if (MetaData(baked).EntityPrototype?.ID == ent.Comp.Companion.Id)
                    QueueDel(baked);
            }

            _tileEnts.Clear();

            var seal = Spawn(ent.Comp.Companion, new EntityCoordinates(ent, offsetTile));

            // Anchoring re-parents the seal to the grid, so the door cannot carry it; track instead.
            if (!_transform.AnchorEntity((seal, Transform(seal)), (xform.GridUid.Value, grid), targetTile))
            {
                QueueDel(seal);
                continue;
            }

            ent.Comp.Seals.Add(seal);
        }

        // Meet the door where it is: a re-anchored open door must not spawn sealed tiles
        if (TryComp<AirtightComponent>(ent, out var doorAirtight))
            SyncSeals(ent, doorAirtight.AirBlocked);
    }

    private void SyncSeals(Entity<CMUWideAirtightComponent> ent, bool blocked)
    {
        foreach (var seal in ent.Comp.Seals)
        {
            if (TryComp<AirtightComponent>(seal, out var sealAirtight))
                _airtight.SetAirblocked((seal, sealAirtight), blocked);
        }
    }

    private void OnShutdown(Entity<CMUWideAirtightComponent> ent, ref ComponentShutdown args)
    {
        foreach (var seal in ent.Comp.Seals)
            QueueDel(seal);
    }
}
