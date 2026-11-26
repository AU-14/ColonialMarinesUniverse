using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Placement;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Deploy;

/// <summary>
///     Equipment deployed from the hand in front of the user and packed up with a tool, ported from RMC-14 for
///     planted flags.
/// </summary>
public sealed class CMUDeployableEquipmentSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private CMUPlacementSystem _placement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDeployableEquipmentComponent, UseInHandEvent>(OnDeployableUseInHand);
        SubscribeLocalEvent<CMUDeployableEquipmentComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUDeployableEquipmentComponent, PickupAttemptEvent>(OnDeployablePickupAttempt);
        SubscribeLocalEvent<CMUDeployableEquipmentComponent, CMUEquipmentDeployDoAfterEvent>(OnEquipmentDeployDoAfter);
        SubscribeLocalEvent<CMUDeployableEquipmentComponent, CMUEquipmentUnDeployDoAfterEvent>(OnEquipmentUnDeployDoAfter);
    }

    private void OnDeployableUseInHand(Entity<CMUDeployableEquipmentComponent> ent, ref UseInHandEvent args)
    {
        args.Handled = true;

        if (!CanDeployPopup(ent, args.User, out _, out _))
            return;

        var ev = new CMUEquipmentDeployDoAfterEvent();
        var delay = ent.Comp.DeployDelay;
        if (ent.Comp.DelaySkill is { } delaySkill)
            delay *= _skills.GetSkillDelayMultiplier(args.User, delaySkill);

        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, ev, ent)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnInteractUsing(Entity<CMUDeployableEquipmentComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled) // Another system already used this tool.
            return;

        if (!_tool.HasQuality(args.Used, ent.Comp.UndeployQuality))
            return;

        if (ent.Comp.DeployedState == CMUDeployedState.Undeployed)
            return;

        args.Handled = true;
        var user = args.User;
        var ev = new CMUEquipmentUnDeployDoAfterEvent();
        var delay = ent.Comp.UndeployDelay;
        if (ent.Comp.DelaySkill is { } delaySkill)
            delay *= _skills.GetSkillDelayMultiplier(user, delaySkill);

        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, ent)
        {
            BreakOnMove = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            var selfMsg = Loc.GetString("rmc-sentry-disassemble-start-self", ("sentry", ent));
            var othersMsg = Loc.GetString("rmc-sentry-disassemble-start-others", ("user", user), ("sentry", ent));
            _popup.PopupEntity(selfMsg, othersMsg, ent, user);
        }
    }

    private void OnDeployablePickupAttempt(Entity<CMUDeployableEquipmentComponent> sentry, ref PickupAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (sentry.Comp.DeployedState != CMUDeployedState.Undeployed)
            args.Cancel();
    }

    private void OnEquipmentDeployDoAfter(Entity<CMUDeployableEquipmentComponent> ent, ref CMUEquipmentDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (!CanDeployPopup(ent, args.User, out var coordinates, out var angle))
            return;

        var xform = Transform(ent);
        _transform.SetCoordinates(ent, xform, coordinates, angle);
        if (ent.Comp.AnchorOnDeploy)
            _transform.AnchorEntity(ent, xform);

        ent.Comp.DeployedState = CMUDeployedState.Deployed;
        Dirty(ent);

        var ev = new CMUEquipmentDeployedEvent(args.User, angle);
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnEquipmentUnDeployDoAfter(Entity<CMUDeployableEquipmentComponent> ent, ref CMUEquipmentUnDeployDoAfterEvent args)
    {
        var user = args.User;
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.DeployedState == CMUDeployedState.Undeployed)
            return;

        _transform.Unanchor(ent.Owner, Transform(ent));

        ent.Comp.DeployedState = CMUDeployedState.Undeployed;
        Dirty(ent);

        var ev = new CMUEquipmentUnDeployedEvent();
        RaiseLocalEvent(ent, ref ev);

        var selfMsg = Loc.GetString("rmc-sentry-disassemble-finish-self", ("sentry", ent));
        var othersMsg = Loc.GetString("rmc-sentry-disassemble-finish-others", ("user", user), ("sentry", ent));
        _popup.PopupEntity(selfMsg, othersMsg, ent, user);
    }

    private bool CanDeployPopup(Entity<CMUDeployableEquipmentComponent> deployable, EntityUid user, out EntityCoordinates coordinates, out Angle rotation)
    {
        coordinates = default;
        rotation = default;

        var moverCoordinates = _transform.GetMoverCoordinateRotation(user, Transform(user));
        coordinates = moverCoordinates.Coords;
        rotation = moverCoordinates.worldRot.GetCardinalDir().ToAngle();

        var direction = rotation.GetCardinalDir();
        coordinates = coordinates.Offset(direction.ToVec() * deployable.Comp.DeployDistance);
        // Deployable against barricades while keeping other obstruction checks, like sentries.
        if (!_rmcMap.CanDeployWeaponOn(coordinates))
        {
            var msg = Loc.GetString("rmc-sentry-need-open-area", ("sentry", deployable));
            _popup.PopupEntity(msg, user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_placement.CanPlacePopup(deployable, coordinates, user))
            return false;

        if (HasComp<VehicleInteriorOccupantComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("emplacement-mount-deploy-vehicle"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUEquipmentDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUEquipmentUnDeployDoAfterEvent : SimpleDoAfterEvent;

[ByRefEvent]
public record struct CMUEquipmentDeployedEvent(EntityUid User, Angle Direction, bool Handled = false);

[ByRefEvent]
public record struct CMUEquipmentUnDeployedEvent(bool Handled = false);
