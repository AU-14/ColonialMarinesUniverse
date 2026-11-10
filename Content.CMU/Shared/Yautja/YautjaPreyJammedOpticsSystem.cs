using Content.Shared._RMC14.Visor;
using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;

namespace Content.Shared.CMU14.Yautja;

public sealed class YautjaPreyJammedOpticsSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaPreyJammedOpticsComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaPreyJammedOpticsComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<YautjaPreyJammedOpticsComponent, ActivateVisorEvent>(OnActivateVisor);
        SubscribeLocalEvent<YautjaPreyJammedOpticsComponent, ItemToggleActivateAttemptEvent>(OnToggleAttempt);
    }

    private void OnExamined(Entity<YautjaPreyJammedOpticsComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-yautja-prey-optics-jammed-examine", ("item", ent.Owner)));
    }

    private void OnGotEquipped(Entity<YautjaPreyJammedOpticsComponent> ent, ref GotEquippedEvent args)
    {
        _popup.PopupClient(Loc.GetString("cmu-yautja-prey-optics-jammed-dead", ("item", ent.Owner)), args.EquipTarget, args.EquipTarget, PopupType.SmallCaution);
    }

    // the visor still flips down, it just shows nothing
    private void OnActivateVisor(Entity<YautjaPreyJammedOpticsComponent> ent, ref ActivateVisorEvent args)
    {
        if (args.User is { } user)
            _popup.PopupClient(Loc.GetString("cmu-yautja-prey-optics-jammed-dead", ("item", ent.Owner)), user, user, PopupType.SmallCaution);
    }

    private void OnToggleAttempt(Entity<YautjaPreyJammedOpticsComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        args.Cancelled = true;
        args.Popup = Loc.GetString("cmu-yautja-prey-optics-jammed-dead", ("item", ent.Owner));
    }
}
