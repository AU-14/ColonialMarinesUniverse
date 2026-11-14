using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// ECS-302: a Bad Blood cannot fire human guns. Every Yautja gun carries YautjaTechItemComponent (caster, plasma
// weapons, spike launchers, bow), so anything without it is human-made. Honourable hunters are not touched: the
// handler sits on the Bad Blood marker, not on YautjaComponent.
public sealed partial class BadBloodHumanWeaponSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly TimeSpan PopupCooldown = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<YautjaBadBloodComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled || HasComp<YautjaTechItemComponent>(args.Used.Owner))
            return;

        args.Cancel();

        var time = _timing.CurTime;
        if (time < ent.Comp.NextWeaponRefusePopup)
            return;

        ent.Comp.NextWeaponRefusePopup = time + PopupCooldown;
        _popup.PopupClient(Loc.GetString("cmu-yautja-weapon-refuse"), ent.Owner, ent.Owner, PopupType.SmallCaution);
    }
}
