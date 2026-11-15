using Content.Shared.Doors;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

public sealed class GunshipDoorGunnerDoorSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<GunshipDoorGunnerHeldOpenComponent, BeforeDoorClosedEvent>(OnBeforeClosed);
        SubscribeLocalEvent<GunshipDoorGunnerHeldOpenComponent, BeforeDoorAutoCloseEvent>(OnBeforeAutoClose);
    }

    private void OnBeforeClosed(Entity<GunshipDoorGunnerHeldOpenComponent> ent, ref BeforeDoorClosedEvent args)
    {
        // The client must reject predicted closing before it starts the door animation.
        if (ent.Comp.HeldOpen)
            args.Cancel();
    }

    private void OnBeforeAutoClose(Entity<GunshipDoorGunnerHeldOpenComponent> ent, ref BeforeDoorAutoCloseEvent args)
    {
        if (ent.Comp.HeldOpen)
            args.Cancel();
    }
}
