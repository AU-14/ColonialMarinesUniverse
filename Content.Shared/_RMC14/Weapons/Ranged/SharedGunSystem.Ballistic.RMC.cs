using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.DoAfter;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private void InitializeRmcBallistic()
    {
        SubscribeLocalEvent<BallisticAmmoProviderComponent, DelayedAmmoInsertDoAfterEvent>(OnRmcDelayedAmmoInsert);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, DelayedCycleDoAfterEvent>(OnRmcDelayedCycle);
    }

    private void CycleBallisticWithRmcDelay(Entity<BallisticAmmoProviderComponent> ent, EntityUid user)
    {
        if (ent.Comp.CycleDelay <= TimeSpan.Zero)
        {
            ManualCycle(ent, TransformSystem.GetMapCoordinates(ent), user);
            return;
        }

        PopupSystem.PopupEntity(
            Loc.GetString("gun-ballistic-cycle-delayed", ("entity", ent.Owner)),
            ent,
            user);

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            ent.Comp.CycleDelay,
            new DelayedCycleDoAfterEvent(),
            eventTarget: ent,
            target: ent,
            used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true,
        });
    }

    private void OnRmcDelayedAmmoInsert(
        Entity<BallisticAmmoProviderComponent> ent,
        ref DelayedAmmoInsertDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled || args.Used is not { } ammo)
        {
            PopupSystem.PopupEntity(Loc.GetString("gun-ballistic-transfer-cancelled"), ent, args.User);
            return;
        }

        if (!CanInsertBallistic(ent, ammo) ||
            !TryBallisticInsert(ent, ammo, args.User))
        {
            return;
        }

        args.Handled = true;
    }

    private void OnRmcDelayedCycle(
        Entity<BallisticAmmoProviderComponent> ent,
        ref DelayedCycleDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            PopupSystem.PopupEntity(
                Loc.GetString("gun-ballistic-cycle-delayed-cancelled", ("entity", ent.Owner)),
                ent,
                args.User);
            return;
        }

        if (GetBallisticShots(ent.Comp) == 0)
        {
            PopupSystem.PopupEntity(
                Loc.GetString("gun-ballistic-cycle-delayed-empty", ("entity", ent.Owner)),
                ent,
                args.User);
            return;
        }

        args.Handled = true;
        ManualCycle(ent, TransformSystem.GetMapCoordinates(ent), args.User);
    }
}
