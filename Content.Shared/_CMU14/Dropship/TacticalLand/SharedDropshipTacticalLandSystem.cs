using Content.Shared._RMC14.Dropship;
using Content.Shared.Buckle.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

public abstract class SharedDropshipTacticalLandSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<MeleeWeaponComponent, AttemptMeleeEvent>(OnPilotMeleeAttempt);

        Subs.BuiEvents<DropshipNavigationComputerComponent>(DropshipNavigationUiKey.Key,
            subs =>
            {
                subs.Event<DropshipNavigationTacticalLandStartMsg>(OnTacticalLandStart);
                subs.Event<DropshipNavigationTacticalLandConfirmMsg>(OnTacticalLandConfirm);
                subs.Event<DropshipNavigationTacticalLandCancelMsg>(OnTacticalLandCancel);
                subs.Event<DropshipNavigationTacticalLandMoveUpMsg>(OnTacticalLandMoveUp);
                subs.Event<DropshipNavigationTacticalLandMoveDownMsg>(OnTacticalLandMoveDown);
                subs.Event<DropshipNavigationTacticalLandRotateMsg>(OnTacticalLandRotate);
            });
    }

    private void OnPilotMeleeAttempt(Entity<MeleeWeaponComponent> weapon, ref AttemptMeleeEvent args)
    {
        if (!TryComp(args.User, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } seat ||
            !HasComp<GunshipPilotSeatComponent>(seat))
        {
            return;
        }

        // Both hands are occupied by the pilot controls. Cancel only melee so
        // operating the dropship's remote direct-fire weapon remains possible.
        args.Cancelled = true;
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

    protected virtual void OnTacticalLandRotate(Entity<DropshipNavigationComputerComponent> ent, ref DropshipNavigationTacticalLandRotateMsg args)
    {
    }

}
