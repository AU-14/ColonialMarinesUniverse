using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._CMU14.Yautja;

public sealed class YautjaTechItemSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTechItemComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<YautjaTechItemComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<YautjaTechItemComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<YautjaTechItemComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<YautjaTechItemComponent, AttemptShootEvent>(OnShootAttempt);
    }

    private void OnPickupAttempt(Entity<YautjaTechItemComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.BlockPickup || IsAllowed(args.User))
            return;

        Misuse(ent.Owner, args.User, YautjaTechMisuseKind.Pickup);
        Deny(args.User);
        args.Cancel();
    }

    private void OnUseInHand(Entity<YautjaTechItemComponent> ent, ref UseInHandEvent args)
    {
        if (!ent.Comp.BlockUse || IsAllowed(args.User))
            return;

        Misuse(ent.Owner, args.User, YautjaTechMisuseKind.Use);
        Deny(args.User);
        args.Handled = true;
    }

    private void OnAttemptMelee(Entity<YautjaTechItemComponent> ent, ref AttemptMeleeEvent args)
    {
        if (!ent.Comp.BlockMelee || IsAllowed(args.User))
            return;

        Misuse(ent.Owner, args.User, YautjaTechMisuseKind.Melee);
        Deny(args.User);
        args.Cancelled = true;
        args.Message = Loc.GetString("cmu-yautja-tech-denied");
    }

    private void OnThrowAttempt(Entity<YautjaTechItemComponent> ent, ref ThrowItemAttemptEvent args)
    {
        if (!ent.Comp.BlockThrow || IsAllowed(args.User))
            return;

        Misuse(ent.Owner, args.User, YautjaTechMisuseKind.Throw);
        Deny(args.User);
        args.Cancelled = true;
    }

    private void OnShootAttempt(Entity<YautjaTechItemComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !ent.Comp.BlockShoot || IsAllowed(args.User))
            return;

        Misuse(ent.Owner, args.User, YautjaTechMisuseKind.Shoot);
        Deny(args.User);
        args.Cancelled = true;
    }

    private bool IsAllowed(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user) ||
               HasComp<BypassInteractionChecksComponent>(user);
    }

    private void Deny(EntityUid user)
    {
        _popup.PopupClient(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
    }

    private void Misuse(EntityUid item, EntityUid user, YautjaTechMisuseKind kind)
    {
        var ev = new YautjaTechMisusedEvent(user, item, kind);
        RaiseLocalEvent(item, ref ev);
    }
}
