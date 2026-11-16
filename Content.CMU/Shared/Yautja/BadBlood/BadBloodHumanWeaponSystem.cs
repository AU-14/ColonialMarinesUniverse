using Content.Shared.Flash.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// ECS-302: a Bad Blood cannot fire human guns. Every Yautja gun carries YautjaTechItemComponent (caster, plasma
// weapons, spike launchers, bow), so anything without it is human-made. Honourable hunters are not touched: the
// handler sits on the Bad Blood marker, not on YautjaComponent.
// Flashes and flashbangs are refused too: a Bad Blood cannot arm a flashbang or fire a handheld flash, in hand or by
// hitting someone with it.
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
    }

    private void OnShotAttempted(Entity<YautjaBadBloodComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled || HasComp<YautjaTechItemComponent>(args.Used.Owner))
            return;

        args.Cancel();
        PopupRefuse(ent);
    }

    // using a flash or flashbang in hand: the flash goes off, the flashbang arms
    private void OnUseAttempt(Entity<YautjaBadBloodComponent> ent, ref UseAttemptEvent args)
    {
        if (args.Cancelled || !IsFlashDevice(args.Used))
            return;

        args.Cancel();
        PopupRefuse(ent);
    }

    // hitting someone with a handheld flash flashes them
    private void OnAttackAttempt(Entity<YautjaBadBloodComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled || args.Weapon is not { } weapon || !IsFlashDevice(weapon.Owner))
            return;

        args.Cancel();
        PopupRefuse(ent);
    }

    public bool IsFlashDevice(EntityUid item)
    {
        return !HasComp<YautjaTechItemComponent>(item) &&
               (HasComp<FlashComponent>(item) || HasComp<FlashOnTriggerComponent>(item));
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
