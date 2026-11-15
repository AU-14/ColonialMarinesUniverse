using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.Fighter;

public sealed partial class CMUManpadLowAltitudeSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private FighterManpadSystem _fighterManpad = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FighterManpadComponent, FighterManpadAimStartedEvent>(OnFighterAimStarted);
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, CMUManpadLowAltitudeAimActionEvent>(OnAim);
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, ItemUnwieldedEvent>(OnUnwielded);
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, HandDeselectedEvent>(OnDeselected);
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnGetActions(Entity<CMUManpadLowAltitudeComponent> ent, ref GetItemActionsEvent args)
    {
        if (!args.InHands)
            return;

        args.AddAction(ref ent.Comp.AimAction, ent.Comp.AimActionId);
        Dirty(ent);
    }

    private void OnAim(Entity<CMUManpadLowAltitudeComponent> ent, ref CMUManpadLowAltitudeAimActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.AimingUser == args.Performer)
        {
            StopAiming(ent);
            _popup.PopupEntity(Loc.GetString("cmu-manpad-low-altitude-lowered"), args.Performer, args.Performer);
            return;
        }

        if (!CanAim(ent, args.Performer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-manpad-low-altitude-requires-wield"), args.Performer, args.Performer);
            return;
        }

        if (!HasAmmo(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-manpad-low-altitude-empty"), args.Performer, args.Performer);
            return;
        }

        // one mode at a time
        if (TryComp(ent, out FighterManpadComponent? fighter))
            _fighterManpad.StopAiming((ent, fighter));

        ent.Comp.AimingUser = args.Performer;
        _actions.SetToggled(ent.Comp.AimAction, true);
        Dirty(ent);

        var started = new CMUManpadLowAltitudeAimStartedEvent(args.Performer);
        RaiseLocalEvent(ent, ref started);
        _popup.PopupEntity(Loc.GetString("cmu-manpad-low-altitude-raised"), args.Performer, args.Performer);
    }

    private void OnFighterAimStarted(Entity<FighterManpadComponent> ent, ref FighterManpadAimStartedEvent args)
    {
        if (TryComp(ent, out CMUManpadLowAltitudeComponent? lowAltitude))
            StopAiming((ent, lowAltitude));
    }

    private void OnAttemptShoot(Entity<CMUManpadLowAltitudeComponent> ent, ref AttemptShootEvent args)
    {
        args.Cancelled = true;

        if (ent.Comp.AimingUser != args.User)
            return;

        if (ent.Comp.LockState != CMUManpadLowAltitudeLockState.Locked)
        {
            args.Message = Loc.GetString("cmu-manpad-low-altitude-no-lock");
            return;
        }

        var launch = new CMUManpadLowAltitudeLaunchRequestEvent(args.User);
        RaiseLocalEvent(ent, ref launch);
    }

    public bool CanAim(Entity<CMUManpadLowAltitudeComponent> ent, EntityUid user)
    {
        return !TerminatingOrDeleted(user)
               && _hands.GetActiveItem(user) == ent.Owner
               && TryComp(ent, out WieldableComponent? wieldable)
               && wieldable.Wielded
               && !_containers.IsEntityOrParentInContainer(user)
               && _blocker.CanInteract(user, ent)
               && _blocker.CanUseHeldEntity(user, ent);
    }

    public bool IsAiming(Entity<CMUManpadLowAltitudeComponent> ent)
    {
        return ent.Comp.AimingUser is { } user && CanAim(ent, user) && HasAmmo(ent);
    }

    public bool HasAmmo(EntityUid launcher)
    {
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(launcher, ref ammo);
        return ammo.Count > 0;
    }

    public void StopAiming(Entity<CMUManpadLowAltitudeComponent> ent)
    {
        if (ent.Comp.AimingUser == null)
            return;

        ent.Comp.AimingUser = null;
        ent.Comp.LockState = CMUManpadLowAltitudeLockState.None;
        ent.Comp.TargetKind = CMUManpadLowAltitudeTargetKind.None;
        _actions.SetToggled(ent.Comp.AimAction, false);
        Dirty(ent);

        var stopped = new CMUManpadLowAltitudeAimStoppedEvent();
        RaiseLocalEvent(ent, ref stopped);
    }

    public void SetLock(
        Entity<CMUManpadLowAltitudeComponent> ent,
        CMUManpadLowAltitudeLockState state,
        CMUManpadLowAltitudeTargetKind kind)
    {
        if (ent.Comp.LockState == state && ent.Comp.TargetKind == kind)
            return;

        ent.Comp.LockState = state;
        ent.Comp.TargetKind = kind;
        Dirty(ent);
    }

    public string GetTargetName(CMUManpadLowAltitudeTargetKind kind)
    {
        return Loc.GetString(kind switch
        {
            CMUManpadLowAltitudeTargetKind.Gunship => "cmu-manpad-low-altitude-target-gunship",
            CMUManpadLowAltitudeTargetKind.Blackfoot => "cmu-manpad-low-altitude-target-blackfoot",
            _ => "cmu-manpad-low-altitude-target-none",
        });
    }

    private void OnUnwielded(Entity<CMUManpadLowAltitudeComponent> ent, ref ItemUnwieldedEvent args)
        => StopAiming(ent);

    private void OnUnequipped(Entity<CMUManpadLowAltitudeComponent> ent, ref GotUnequippedHandEvent args)
        => StopAiming(ent);

    private void OnDeselected(Entity<CMUManpadLowAltitudeComponent> ent, ref HandDeselectedEvent args)
        => StopAiming(ent);

    private void OnExamined(Entity<CMUManpadLowAltitudeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.AimingUser == null)
            return;

        var key = ent.Comp.LockState switch
        {
            CMUManpadLowAltitudeLockState.Locking => "cmu-manpad-low-altitude-examine-locking",
            CMUManpadLowAltitudeLockState.Locked => "cmu-manpad-low-altitude-examine-locked",
            _ => "cmu-manpad-low-altitude-examine-aimed",
        };
        args.PushMarkup(Loc.GetString(key, ("target", GetTargetName(ent.Comp.TargetKind))));
    }
}
