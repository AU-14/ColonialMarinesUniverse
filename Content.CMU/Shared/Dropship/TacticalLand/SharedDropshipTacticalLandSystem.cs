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
                subs.Event<DropshipNavigationTacticalLandMoveUpMsg>(OnTacticalLandMoveUp);
                subs.Event<DropshipNavigationTacticalLandMoveDownMsg>(OnTacticalLandMoveDown);
                subs.Event<DropshipNavigationTacticalHoverCancelMsg>(OnTacticalHoverCancel);
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

    protected virtual void OnTacticalLandMoveUp(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandMoveUpMsg args)
    {
    }

    protected virtual void OnTacticalLandMoveDown(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandMoveDownMsg args)
    {
    }

    protected virtual void OnTacticalHoverCancel(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalHoverCancelMsg args)
    {
    }
}
