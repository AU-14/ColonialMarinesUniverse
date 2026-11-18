using Content.Shared._RMC14.Stamina;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Flash.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// ECS-302: a Bad Blood cannot fire human guns. Every Yautja gun carries YautjaTechItemComponent (caster, plasma
// weapons, spike launchers, bow), so anything without it is human-made. Honourable hunters are not touched: the
// handler sits on the Bad Blood marker, not on YautjaComponent.
// Flashes and flashbangs are refused too: a Bad Blood cannot arm a flashbang or fire a handheld flash, in hand or by
// hitting someone with it.
// CMU14 ECS-603: so are the other MP tools: cuffs and zipties, stun batons and prods, and human grenades. Generic
// human melee (crowbars, knives) stays allowed, and so does gear the clan made (BadBloodGearLock), like the
// Engineer's demolition charge.
public sealed partial class BadBloodHumanWeaponSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly TimeSpan PopupCooldown = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<YautjaBadBloodComponent, UseAttemptEvent>(OnUseAttempt);
        SubscribeLocalEvent<YautjaBadBloodComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<YautjaBadBloodComponent, ThrowAttemptEvent>(OnThrowAttempt);
    }

    private void OnShotAttempted(Entity<YautjaBadBloodComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled || HasComp<YautjaTechItemComponent>(args.Used.Owner))
            return;

        args.Cancel();
        PopupRefuse(ent);
    }

    // using an MP tool in hand or on someone: the flash goes off, the grenade arms, the cuffs go on
    private void OnUseAttempt(Entity<YautjaBadBloodComponent> ent, ref UseAttemptEvent args)
    {
        if (args.Cancelled || !IsForbiddenHumanTool(args.Used))
            return;

        args.Cancel();
        PopupRefuse(ent);
    }

    // hitting someone with an MP tool: a flash flashes them, a baton stuns them, cuffs go on
    private void OnAttackAttempt(Entity<YautjaBadBloodComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled || args.Weapon is not { } weapon || !IsForbiddenHumanTool(weapon.Owner))
            return;

        args.Cancel();
        PopupRefuse(ent);
    }

    // CMU14 ECS-603: a thrown baton still stuns on impact (RMCStaminaDamageOnCollide), so it cannot be thrown either
    private void OnThrowAttempt(Entity<YautjaBadBloodComponent> ent, ref ThrowAttemptEvent args)
    {
        if (args.Cancelled || HasComp<YautjaTechItemComponent>(args.ItemUid) ||
            HasComp<BadBloodGearLockComponent>(args.ItemUid) || !IsStunBaton(args.ItemUid))
        {
            return;
        }

        args.Cancel();
        PopupRefuse(ent);
    }

    // CMU14 ECS-603: human-made (no YautjaTechItem, no BadBloodGearLock) flashes, cuffs, batons and grenades
    public bool IsForbiddenHumanTool(EntityUid item)
    {
        if (HasComp<YautjaTechItemComponent>(item) || HasComp<BadBloodGearLockComponent>(item))
            return false;

        return HasComp<FlashComponent>(item) ||
               HasComp<FlashOnTriggerComponent>(item) ||
               HasComp<HandcuffComponent>(item) ||
               IsStunBaton(item) ||
               HasComp<TriggerOnUseComponent>(item);
    }

    // a baton or prod is a toggled melee weapon that stuns. Either half alone is too wide for D8: welders, lighters,
    // laser scalpels and entrenching tools toggle, and chairs and the provost's lawbook stun on hit
    private bool IsStunBaton(EntityUid item)
    {
        return HasComp<ItemToggleMeleeWeaponComponent>(item) &&
               (HasComp<RMCStaminaDamageOnHitComponent>(item) || HasComp<StaminaDamageOnHitComponent>(item));
    }

    private void PopupRefuse(Entity<YautjaBadBloodComponent> ent)
    {
        var time = _timing.CurTime;
        if (time < ent.Comp.NextWeaponRefusePopup)
            return;

        ent.Comp.NextWeaponRefusePopup = time + PopupCooldown;
        _popup.PopupClient(Loc.GetString("cmu-yautja-weapon-refuse"), ent.Owner, ent.Owner, PopupType.SmallCaution);
    }
}
