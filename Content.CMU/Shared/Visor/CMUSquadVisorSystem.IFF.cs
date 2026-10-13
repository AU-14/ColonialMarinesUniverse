using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared.CMU14.Visor;

/// <summary>
/// Squad optic IFF: while the optic is down, the wearer's held guns won't fire with a friendly in the line of fire.
/// Bullets still hit friendlies they stray into. The guns are a little less accurate and lose damage over range a
/// little faster while it's on. Both this and the grenade warnings have an action to switch them off.
/// </summary>
public sealed partial class CMUSquadVisorSystem
{
    private void InitializeIFF()
    {
        SubscribeLocalEvent<CMUSquadVisorWearerComponent, CMUToggleSquadVisorIFFActionEvent>(OnToggleIff);
        SubscribeLocalEvent<CMUSquadVisorWearerComponent, CMUToggleSquadVisorGrenadeWarningsActionEvent>(OnToggleGrenadeWarnings);
        SubscribeLocalEvent<CMUSquadVisorWearerComponent, CMUToggleSquadVisorMotionLinkActionEvent>(OnToggleMotionLink);
        SubscribeLocalEvent<CMUSquadVisorWearerComponent, DidEquipHandEvent>(OnWearerPickedUp);

        SubscribeLocalEvent<CMUSquadVisorIFFGunComponent, GotUnequippedHandEvent>(OnGunDropped);
        SubscribeLocalEvent<CMUSquadVisorIFFGunComponent, AttemptShootEvent>(OnGunAttemptShoot);
        SubscribeLocalEvent<CMUSquadVisorIFFGunComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<CMUSquadVisorIFFGunComponent, GetDamageFalloffEvent>(OnGunGetDamageFalloff);
    }

    private void AddToggleActions(EntityUid wearer, CMUSquadVisorWearerComponent marker)
    {
        if (_net.IsClient)
            return;

        _actions.AddAction(wearer, ref marker.IffAction, marker.IffActionId);
        _actions.AddAction(wearer, ref marker.GrenadeAction, marker.GrenadeActionId);
        _actions.AddAction(wearer, ref marker.MotionLinkAction, marker.MotionLinkActionId);
        _actions.SetToggled(marker.IffAction, marker.IffEnabled);
        _actions.SetToggled(marker.GrenadeAction, marker.GrenadeWarningsEnabled);
        _actions.SetToggled(marker.MotionLinkAction, marker.MotionLinkEnabled);
        Dirty(wearer, marker);
    }

    private void RemoveToggleActions(EntityUid wearer, CMUSquadVisorWearerComponent marker)
    {
        if (_net.IsClient)
            return;

        foreach (var action in new[] { marker.IffAction, marker.GrenadeAction, marker.MotionLinkAction })
        {
            if (action is not { } uid)
                continue;

            _actions.RemoveAction(wearer, uid);
            QueueDel(uid);
        }

        marker.IffAction = null;
        marker.GrenadeAction = null;
        marker.MotionLinkAction = null;
    }

    private void OnToggleIff(Entity<CMUSquadVisorWearerComponent> ent, ref CMUToggleSquadVisorIFFActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.IffEnabled = !ent.Comp.IffEnabled;
        Dirty(ent);

        _actions.SetToggled(ent.Comp.IffAction, ent.Comp.IffEnabled);
        _popup.PopupClient(Loc.GetString(ent.Comp.IffEnabled ? "cmu-squad-visor-iff-on" : "cmu-squad-visor-iff-off"),
            ent, ent, PopupType.Small);

        UpdateHeldGuns(ent);
    }

    private void OnToggleGrenadeWarnings(Entity<CMUSquadVisorWearerComponent> ent, ref CMUToggleSquadVisorGrenadeWarningsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.GrenadeWarningsEnabled = !ent.Comp.GrenadeWarningsEnabled;
        Dirty(ent);

        _actions.SetToggled(ent.Comp.GrenadeAction, ent.Comp.GrenadeWarningsEnabled);
        _popup.PopupClient(Loc.GetString(ent.Comp.GrenadeWarningsEnabled
                ? "cmu-squad-visor-grenades-on"
                : "cmu-squad-visor-grenades-off"),
            ent, ent, PopupType.Small);
    }

    private void OnToggleMotionLink(Entity<CMUSquadVisorWearerComponent> ent, ref CMUToggleSquadVisorMotionLinkActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.MotionLinkEnabled = !ent.Comp.MotionLinkEnabled;
        Dirty(ent);

        _actions.SetToggled(ent.Comp.MotionLinkAction, ent.Comp.MotionLinkEnabled);
        _popup.PopupClient(Loc.GetString(ent.Comp.MotionLinkEnabled
                ? "cmu-squad-visor-motion-link-on"
                : "cmu-squad-visor-motion-link-off"),
            ent, ent, PopupType.Small);
    }

    private void OnWearerPickedUp(Entity<CMUSquadVisorWearerComponent> ent, ref DidEquipHandEvent args)
    {
        SwitchUpIfSmartgun(ent);
        UpdateHeldGuns(ent);
    }

    private void OnGunDropped(Entity<CMUSquadVisorIFFGunComponent> gun, ref GotUnequippedHandEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RemComp<CMUSquadVisorIFFGunComponent>(gun);
        _gun.RefreshModifiers(gun.Owner);
    }

    /// <summary>
    /// Puts the IFF on every gun the mob holds while it's looking through an optic with IFF on, and takes it off
    /// otherwise.
    /// </summary>
    private void UpdateHeldGuns(EntityUid mob)
    {
        if (_timing.ApplyingState)
            return;

        var active = TryComp<CMUSquadVisorWearerComponent>(mob, out var marker) &&
                     marker.LifeStage <= ComponentLifeStage.Running &&
                     marker.IffEnabled;

        foreach (var held in _hands.EnumerateHeld(mob))
        {
            if (!HasComp<GunComponent>(held))
                continue;

            bool changed;
            if (active)
            {
                changed = !HasComp<CMUSquadVisorIFFGunComponent>(held);
                EnsureComp<CMUSquadVisorIFFGunComponent>(held);
            }
            else
            {
                changed = RemComp<CMUSquadVisorIFFGunComponent>(held);
            }

            if (changed)
                _gun.RefreshModifiers(held);
        }
    }

    /// <summary>
    /// Guns with their own working IFF don't need the optic's, and don't take its penalties.
    /// </summary>
    private bool HasOwnIff(EntityUid gun)
    {
        return TryComp<GunIFFComponent>(gun, out var gunIff) && gunIff.Enabled ||
               TryComp<GunAttachableIFFComponent>(gun, out var attachableIff) && attachableIff.PreventFriendlyFire;
    }

    private bool IsUsingOpticIff(EntityUid gun)
    {
        return !HasOwnIff(gun) &&
               _rmcGun.TryGetGunUser(gun, out var user) &&
               TryComp<CMUSquadVisorWearerComponent>(user, out var marker) &&
               marker.IffEnabled;
    }

    private void OnGunAttemptShoot(Entity<CMUSquadVisorIFFGunComponent> gun, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !IsUsingOpticIff(gun))
            return;

        _attachableIff.CheckPreventFriendlyFire(gun, ref args);
    }

    private void OnGunRefreshModifiers(Entity<CMUSquadVisorIFFGunComponent> gun, ref GunRefreshModifiersEvent args)
    {
        if (!IsUsingOpticIff(gun))
            return;

        args.MinAngle = new Angle(args.MinAngle.Theta * gun.Comp.SpreadMultiplier);
        args.MaxAngle = new Angle(args.MaxAngle.Theta * gun.Comp.SpreadMultiplier);
    }

    private void OnGunGetDamageFalloff(Entity<CMUSquadVisorIFFGunComponent> gun, ref GetDamageFalloffEvent args)
    {
        if (!IsUsingOpticIff(gun))
            return;

        args.FalloffMultiplier += FixedPoint2.New(gun.Comp.FalloffAdd);
    }
}
