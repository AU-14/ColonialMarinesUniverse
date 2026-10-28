using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaTechItemSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        // CMU14: the melee health-damage modifier is removed (pred2 #10); the handler is kept commented out below.
        SubscribeLocalEvent<YautjaTechItemComponent, StaminaMeleeHitEvent>(OnStaminaMeleeHit);
        SubscribeLocalEvent<YautjaTechItemComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<YautjaTechItemComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<YautjaTechItemComponent, InteractHandEvent>(OnInteractHand, before: [typeof(SharedItemSystem)]);
        SubscribeLocalEvent<YautjaTechItemComponent, UseInHandEvent>(OnUseInHand);
        // CMU14: items.dm:183 does AddElement(/datum/element/yautja_tracked_item) for every item whose
        // flags_item carries ITEM_PREDATOR, i.e. all Yautja gear is registered for tracking the moment it
        // exists - no marking required. The element then keeps it in GLOB.loose_yautja_gear whenever it is
        // not in an honorable carrier's hands, which is what track_gear_internal (yaut_bracers.dm:693) reads.
        // Mirror that here so gear stripped off a dead Yautja is locatable; an item the owner deliberately
        // hid via "Remove Item from Tracker" (YautjaUntrackedItemComponent) stays out of the readout.
        SubscribeLocalEvent<YautjaTechItemComponent, MapInitEvent>(OnTechItemMapInit);
        SubscribeLocalEvent<YautjaTechItemComponent, AttemptMeleeEvent>(OnAttemptMelee);
        SubscribeLocalEvent<YautjaTechItemComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
        SubscribeLocalEvent<YautjaTechItemComponent, AttemptShootEvent>(OnShootAttempt, before: [typeof(SharedRMCFlamerSystem)]);
    }

    // The old per-weapon YautjaTechItem melee modifier is gone: it scaled melee *health* damage, which CM-SS13 does
    // not do - a Yautja weapon gets no melee damage bonus - and melee scaling now belongs to the xeno melee
    // configuration in CMArmorSystem. DamageMultiplier stays for the projectile and melee-stamina paths.
    // }

    private void OnProjectileHit(Entity<YautjaTechItemComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Handled ||
            ent.Comp.DamageMultiplier == 1f ||
            !args.Damage.AnyPositive())
        {
            return;
        }

        args.Damage *= ent.Comp.DamageMultiplier;
    }

    private void OnStaminaMeleeHit(Entity<YautjaTechItemComponent> ent, ref StaminaMeleeHitEvent args)
    {
        if (ent.Comp.DamageMultiplier == 1f)
            return;

        args.Multiplier *= ent.Comp.DamageMultiplier;
    }

    // attempt events are also asked by verb lists, examine and context menus, so this one only blocks;
    // the zap waits for an actual grab in OnInteractHand
    private void OnPickupAttempt(Entity<YautjaTechItemComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (!ent.Comp.BlockPickup || IsAllowed(args.User))
            return;

        args.Cancel();
    }

    private void OnInteractHand(Entity<YautjaTechItemComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.BlockPickup || IsAllowed(args.User))
            return;

        Misuse(ent.Owner, args.User, YautjaTechMisuseKind.Pickup);
        Deny(args.User);
        args.Handled = true;
    }

    private void OnTechItemMapInit(Entity<YautjaTechItemComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<YautjaUntrackedItemComponent>(ent.Owner))
            return;

        EnsureComp<YautjaTrackedItemComponent>(ent.Owner);
    }

    private void OnUseInHand(Entity<YautjaTechItemComponent> ent, ref UseInHandEvent args)
    {
        // CMU14: CM-SS13 only ever gates a gun's able_to_fire, wielding one is free even for a human
        // (SharedWieldableSystem wields through this same event), so a wieldable item is never refused.
        if (HasComp<WieldableComponent>(ent.Owner))
            return;

        if (!ent.Comp.BlockUse ||
            IsAllowed(args.User) ||
            ent.Comp.AllowNonYautjaActiveHandUse && _hands.GetActiveItem(args.User) == ent.Owner)
        {
            return;
        }

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
        args.Cancelled = true;
        args.Message = Loc.GetString(ent.Comp.ShootDeniedPopup);
    }

    private bool IsAllowed(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user) ||
               HasComp<BypassInteractionChecksComponent>(user);
    }

    private void Deny(EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
    }

    private void Misuse(EntityUid item, EntityUid user, YautjaTechMisuseKind kind)
    {
        var ev = new YautjaTechMisusedEvent(user, item, kind);
        RaiseLocalEvent(item, ref ev);
    }
}
