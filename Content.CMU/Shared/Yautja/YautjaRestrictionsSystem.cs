using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Shared restrictions for all Yautja: human guns refuse them (use and pickup), and
/// raised thralls carry a visible corruption on examine.
/// </summary>
public sealed class YautjaRestrictionsSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<YautjaComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<YautjaThrallComponent, ExaminedEvent>(OnThrallExamined);
    }

    private void OnShotAttempted(Entity<YautjaComponent> ent, ref ShotAttemptedEvent args)
    {
        if (HasComp<YautjaTechItemComponent>(args.Used))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("cmu-yautja-weapon-refuse"), ent, ent, PopupType.SmallCaution);
    }

    private void OnPickupAttempt(Entity<YautjaComponent> ent, ref PickupAttemptEvent args)
    {
        if (!HasComp<GunComponent>(args.Item) || HasComp<YautjaTechItemComponent>(args.Item))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("cmu-yautja-weapon-refuse"), ent, ent, PopupType.SmallCaution);
    }

    private void OnThrallExamined(Entity<YautjaThrallComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Raised)
            args.PushMarkup(Loc.GetString("cmu-yautja-thrall-raised-examine"));
    }
}
