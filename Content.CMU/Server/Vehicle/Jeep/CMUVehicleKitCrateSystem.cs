using System.Numerics;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Storage.Components;

namespace Content.Server.CMU14.Vehicle.Jeep;

/// <summary>
/// Opens a vehicle kit crate into its chassis, with the parts, tools and manual it held set down in
/// two rows beside it.
/// </summary>
public sealed class CMUVehicleKitCrateSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleKitCrateComponent, StorageBeforeOpenEvent>(OnBeforeOpen);
        SubscribeLocalEvent<CMUVehicleKitCrateComponent, StorageAfterOpenEvent>(OnAfterOpen);
    }

    // Only someone opening it unpacks it: storages are also opened and shut again as they spawn, to
    // take in what lies on their tile.
    private void OnBeforeOpen(Entity<CMUVehicleKitCrateComponent> ent, ref StorageBeforeOpenEvent args)
    {
        ent.Comp.Contents.Clear();
        if (args.User == null)
            return;

        if (TryComp(ent, out EntityStorageComponent? storage))
            ent.Comp.Contents.AddRange(storage.Contents.ContainedEntities);
    }

    private void OnAfterOpen(Entity<CMUVehicleKitCrateComponent> ent, ref StorageAfterOpenEvent args)
    {
        if (args.User == null || TerminatingOrDeleted(ent))
            return;

        var xform = Transform(ent);
        var origin = xform.Coordinates;
        var chassis = Spawn(ent.Comp.Chassis, origin);
        _transform.SetWorldRotation(chassis, Angle.Zero);

        // The chassis faces south, along y; the contents go down its left and right sides.
        var count = ent.Comp.Contents.Count;
        var perSide = (count + 1) / 2;
        for (var i = 0; i < count; i++)
        {
            var item = ent.Comp.Contents[i];
            if (TerminatingOrDeleted(item))
                continue;

            var side = i % 2 == 0 ? -1f : 1f;
            var row = i / 2;
            var along = perSide <= 1 ? 0f : 0.9f - 1.8f * row / (perSide - 1);
            _transform.SetCoordinates(item, origin.Offset(new Vector2(side * ent.Comp.LayOut, along)));
        }

        ent.Comp.Contents.Clear();
        QueueDel(ent);
    }
}
