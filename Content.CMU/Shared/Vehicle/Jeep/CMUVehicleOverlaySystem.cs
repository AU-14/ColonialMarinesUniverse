using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed class CMUVehicleOverlaySystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleOverlayComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUVehicleOverlayComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<CMUVehicleOverlayComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.Overlay is { } existing && Exists(existing))
            return;

        var overlay = SpawnAttachedTo(ent.Comp.Prototype, new EntityCoordinates(ent, default));
        var follower = EnsureComp<CMUZVisualFollowerComponent>(overlay);
        follower.Target = ent;
        Dirty(overlay, follower);

        ent.Comp.Overlay = overlay;
        Dirty(ent);
    }

    private void OnShutdown(Entity<CMUVehicleOverlayComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient || ent.Comp.Overlay is not { } overlay)
            return;

        QueueDel(overlay);
        ent.Comp.Overlay = null;
    }
}
