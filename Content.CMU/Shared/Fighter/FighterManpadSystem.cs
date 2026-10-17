using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.Fighter;

public sealed partial class FighterManpadSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FighterIFFSystem _iff = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FighterManpadComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<FighterManpadComponent, FighterManpadAimActionEvent>(OnAim);
        SubscribeLocalEvent<FighterManpadComponent, ItemUnwieldedEvent>(OnUnwielded);
        SubscribeLocalEvent<FighterManpadComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<FighterManpadComponent, HandDeselectedEvent>(OnDeselected);
        SubscribeLocalEvent<FighterManpadComponent, ExaminedEvent>(OnExamined);
    }

    private void OnGetActions(Entity<FighterManpadComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands)
            return;

        args.AddAction(ref ent.Comp.AimAction, ent.Comp.AimActionId);
        Dirty(ent);
    }

    private void OnAim(Entity<FighterManpadComponent> ent, ref FighterManpadAimActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.AimingUser == args.Performer)
        {
            StopAiming(ent);
            _popup.PopupEntity(Loc.GetString("cmu-manpad-lowered"), args.Performer, args.Performer);
            return;
        }

        if (!CanAim(ent, args.Performer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-manpad-requires-wield"), args.Performer, args.Performer);
            return;
        }

        if (!HasAmmo(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-manpad-empty"), args.Performer, args.Performer);
            return;
        }

        var faction = _iff.GetOperatorFaction(args.Performer);
        if (faction == null && !ent.Comp.IgnoreIFF)
        {
            _popup.PopupEntity(Loc.GetString("cmu-manpad-iff-unknown"), args.Performer, args.Performer);
            return;
        }
        ent.Comp.Faction = faction;
        ent.Comp.AimingUser = args.Performer;
        _actions.SetToggled(ent.Comp.AimAction, true);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.IgnoreIFF ? "cmu-manpad-aiming-all" : "cmu-manpad-aiming"), args.Performer, args.Performer);
    }

    public bool CanAim(Entity<FighterManpadComponent> ent, EntityUid user)
    {
        return !TerminatingOrDeleted(user) &&
               _hands.GetActiveItem(user) == ent.Owner &&
               TryComp(ent, out WieldableComponent? wieldable) && wieldable.Wielded &&
               !_containers.IsEntityOrParentInContainer(user) &&
               _blocker.CanInteract(user, ent) && _blocker.CanUseHeldEntity(user, ent);
    }

    public bool IsAiming(Entity<FighterManpadComponent> ent) =>
        ent.Comp.AimingUser is { } user && CanAim(ent, user) && HasAmmo(ent) &&
        (ent.Comp.IgnoreIFF || FighterIFFSystem.Same(ent.Comp.Faction, _iff.GetOperatorFaction(user)));

    public bool HasAmmo(EntityUid launcher)
    {
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(launcher, ref ammo);
        return ammo.Count > 0;
    }

    public void StopAiming(Entity<FighterManpadComponent> ent)
    {
        if (ent.Comp.AimingUser == null)
            return;

        ent.Comp.AimingUser = null;
        _actions.SetToggled(ent.Comp.AimAction, false);
        Dirty(ent);
        var ev = new FighterManpadAimStoppedEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnUnwielded(Entity<FighterManpadComponent> ent, ref ItemUnwieldedEvent args) => StopAiming(ent);
    private void OnUnequipped(Entity<FighterManpadComponent> ent, ref GotUnequippedHandEvent args) => StopAiming(ent);
    private void OnDeselected(Entity<FighterManpadComponent> ent, ref HandDeselectedEvent args) => StopAiming(ent);

    private void OnExamined(Entity<FighterManpadComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.IgnoreIFF)
            args.PushMarkup(Loc.GetString("cmu-manpad-examine-no-iff"));
        args.PushMarkup(Loc.GetString(ent.Comp.AimingUser == null ? "cmu-manpad-examine-safe" : "cmu-manpad-examine-aimed"));
    }
}
