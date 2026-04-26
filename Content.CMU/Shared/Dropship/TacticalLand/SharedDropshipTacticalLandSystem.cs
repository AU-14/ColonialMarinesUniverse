using Content.Shared._RMC14.Dropship;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

public abstract class SharedDropshipTacticalLandSystem : EntitySystem
{
    public override void Initialize()
    {
        Subs.BuiEvents<DropshipNavigationComputerComponent>(DropshipNavigationUiKey.Key,
            subs =>
            {
                subs.Event<DropshipNavigationTacticalLandStartMsg>(OnTacticalLandStart);
                subs.Event<DropshipNavigationTacticalLandConfirmMsg>(OnTacticalLandConfirm);
                subs.Event<DropshipNavigationTacticalLandCancelMsg>(OnTacticalLandCancel);
            });
    }

    protected virtual void OnTacticalLandStart(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandStartMsg args)
    {
    }

    protected virtual void OnTacticalLandConfirm(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandConfirmMsg args)
    {
    }

    protected virtual void OnTacticalLandCancel(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandCancelMsg args)
    {
    }
}
