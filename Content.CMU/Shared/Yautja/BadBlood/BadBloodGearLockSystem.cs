using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// Enforces <see cref="BadBloodGearLockComponent"/>: only a Yautja (or someone the hunters authorized, or an admin
/// ghost) can wear, swing, throw, fire or use Bad Blood gear. Picking it up stays open to everyone.
/// </summary>
public sealed class BadBloodGearLockSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    private const string Denied = "cmu-bad-blood-gear-denied";

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodGearLockComponent, BeingEquippedAttemptEvent>(OnBeingEquipped);
        SubscribeLocalEvent<BadBloodGearLockComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<BadBloodGearLockComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<BadBloodGearLockComponent, AttemptShootEvent>(OnShootAttempt, before: [typeof(SharedRMCFlamerSystem)]);
        SubscribeLocalEvent<BadBloodGearLockComponent, UseInHandEvent>(OnUseInHand, before: [typeof(YautjaTechItemSystem)]);
    }

    /// <summary>
    /// Whether <paramref name="user"/> may use Bad Blood gear.
    /// </summary>
    public bool IsAllowed(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user) ||
               HasComp<BypassInteractionChecksComponent>(user);
    }

    // the wearer is what matters: a Yautja may not dress a human in it either
    private void OnBeingEquipped(Entity<BadBloodGearLockComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled || IsAllowed(args.EquipTarget))
            return;

        args.Reason = Denied;
        args.Cancel();
    }

    private void OnAttemptMelee(Entity<BadBloodGearLockComponent> ent, ref AttemptMeleeEvent args)
    {
        if (args.Cancelled || IsAllowed(args.User))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString(Denied);
    }

    private void OnThrowAttempt(Entity<BadBloodGearLockComponent> ent, ref ThrowItemAttemptEvent args)
    {
        if (args.Cancelled || IsAllowed(args.User))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString(Denied), args.User, args.User, PopupType.SmallCaution);
    }

    private void OnShootAttempt(Entity<BadBloodGearLockComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || IsAllowed(args.User))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString(Denied);
    }

    private void OnUseInHand(Entity<BadBloodGearLockComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || IsAllowed(args.User))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString(Denied), args.User, args.User, PopupType.SmallCaution);
    }
}
