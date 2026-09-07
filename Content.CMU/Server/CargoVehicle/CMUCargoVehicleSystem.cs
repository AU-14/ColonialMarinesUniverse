using System.Linq;
using System.Numerics;
using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.CMU14.CargoVehicle;
using Content.Shared.Containers;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.PowerLoader;
using Robust.Shared.Containers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CargoVehicle;

public sealed class CMUCargoVehicleSystem : EntitySystem
{
    private const string ReturnActionId = "CMUActionCargoVehicleReturn";
    private const string SelfDestructActionId = "CMUActionCargoVehicleSelfDestruct";
    private const string ToggleBayActionId = "CMUActionCargoVehicleToggleBay";
    private static readonly TimeSpan SessionValidationInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;
    [Dependency] private VehicleSystem _vehicles = default!;

    private TimeSpan _nextSessionValidation;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUCargoVehicleComponent, ComponentInit>(OnVehicleInit);
        SubscribeLocalEvent<CMUCargoVehicleComponent, DamageChangedEvent>(OnVehicleDamageChanged);
        SubscribeLocalEvent<CMUCargoVehicleComponent, EntityTerminatingEvent>(OnVehicleTerminating);
        SubscribeLocalEvent<CMUCargoVehicleComponent, GetVerbsEvent<AlternativeVerb>>(OnVehicleGetVerbs);
        SubscribeLocalEvent<CMUCargoVehicleComponent, InteractUsingEvent>(OnVehicleInteractUsing);
        SubscribeLocalEvent<CMUCargoVehicleComponent, CMUCargoVehicleLoadDoAfterEvent>(OnLoadDoAfter);
        SubscribeLocalEvent<CMUCargoVehicleComponent, CMUCargoVehicleUnloadDoAfterEvent>(OnUnloadDoAfter);
        SubscribeLocalEvent<CMUCargoVehicleComponent, CMExplosiveTriggeredEvent>(OnVehicleExploded);

        SubscribeLocalEvent<CMUCargoVehicleControllerComponent, ComponentShutdown>(OnControllerShutdown);
        SubscribeLocalEvent<CMUCargoVehicleControllerComponent, EntityTerminatingEvent>(OnControllerTerminating);
        SubscribeLocalEvent<CMUCargoVehicleControllerComponent, GotUnequippedHandEvent>(OnControllerUnequipped);
        SubscribeLocalEvent<CMUCargoVehicleControllerComponent, UseInHandEvent>(OnControllerUseInHand);

        SubscribeLocalEvent<CMUCargoVehicleControlSessionComponent, CMUCargoVehicleReturnActionEvent>(OnReturnAction);
        SubscribeLocalEvent<CMUCargoVehicleControlSessionComponent, CMUCargoVehicleSelfDestructActionEvent>(OnSelfDestructAction);
        SubscribeLocalEvent<CMUCargoVehicleControlSessionComponent, CMUCargoVehicleToggleBayActionEvent>(OnToggleBayAction);
        SubscribeLocalEvent<CMUCargoVehicleControlSessionComponent, PlayerDetachedEvent>(OnVehiclePlayerDetached, after: [typeof(SSDIndicatorSystem)]);

        SubscribeLocalEvent<CMUCargoVehicleRemotePilotComponent, MobStateChangedEvent>(OnOperatorMobStateChanged);
        SubscribeLocalEvent<CMUCargoVehicleRemotePilotComponent, ComponentShutdown>(OnRemotePilotShutdown);
        SubscribeLocalEvent<CMUCargoVehicleRemotePilotComponent, PlayerAttachedEvent>(OnRemotePilotPlayerAttached, after: [typeof(SSDIndicatorSystem)]);
        SubscribeLocalEvent<CMUCargoVehicleRemotePilotComponent, PlayerDetachedEvent>(OnRemotePilotPlayerDetached, after: [typeof(SSDIndicatorSystem)]);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextSessionValidation)
            return;

        _nextSessionValidation = _timing.CurTime + SessionValidationInterval;

        var query = EntityQueryEnumerator<CMUCargoVehicleControlSessionComponent>();
        while (query.MoveNext(out var vehicle, out var session))
        {
            if (!ValidateSession((vehicle, session), out var reason))
                EndControl((vehicle, session), reason);
        }
    }

    private void OnVehicleInit(Entity<CMUCargoVehicleComponent> ent, ref ComponentInit args)
    {
        ent.Comp.CargoContainer = _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.CargoContainerId);

        if (TryComp(ent.Owner, out VehicleComponent? vehicle))
            _vehicles.TrySetOperator((ent.Owner, vehicle), ent.Owner);

        UpdateBayAppearance(ent);
    }

    private void OnVehicleDamageChanged(Entity<CMUCargoVehicleComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.ArmingMode != CMUCargoVehicleArmingMode.None ||
            _damageable.GetTotalDamage((ent.Owner, args.Damageable)) < ent.Comp.AutomaticArmDamage)
        {
            return;
        }

        Arm(ent, CMUCargoVehicleArmingMode.Automatic, null);
    }

    private void OnVehicleTerminating(Entity<CMUCargoVehicleComponent> ent, ref EntityTerminatingEvent args)
    {
        if (TryComp(ent.Owner, out CMUCargoVehicleControlSessionComponent? session))
            EndControl((ent.Owner, session), Loc.GetString("cmu-cargo-vehicle-control-ended-vehicle-lost"));

        UnlinkController(ent);
    }

    private void OnVehicleGetVerbs(Entity<CMUCargoVehicleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Using != null)
            return;

        if (ent.Comp.ArmingMode != CMUCargoVehicleArmingMode.None)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.BayOpen
                ? "cmu-cargo-vehicle-verb-close"
                : "cmu-cargo-vehicle-verb-open"),
            Priority = 3,
            Act = () => ToggleBay(ent, user),
        });

        if (!CanHandleCargo(ent))
            return;

        if (ent.Comp.CargoContainer?.ContainedEntity is { })
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-cargo-vehicle-verb-unload"),
                Priority = 2,
                Act = () => StartUnload(ent, user),
            });
            return;
        }

        if (!TryGetPulledCrate(user, ent.Owner, out var crate))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-cargo-vehicle-verb-load"),
            Priority = 2,
            Act = () => StartLoad(ent, user, crate),
        });
    }

    private void OnVehicleInteractUsing(Entity<CMUCargoVehicleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out CMUCargoVehicleControllerComponent? controller))
            return;

        args.Handled = true;
        PairController(ent, (args.Used, controller), args.User);
    }

    private void OnLoadDoAfter(Entity<CMUCargoVehicleComponent> ent, ref CMUCargoVehicleLoadDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled || args.Used is not { } crate || !CanFinishLoad(ent, args.User, crate))
            return;

        if (TryComp(crate, out PullableComponent? pullable))
            _pulling.TryStopPull(crate, pullable, args.User);

        if (!_containers.Insert(crate, ent.Comp.CargoContainer!))
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-cargo-vehicle-load-finished", ("crate", crate)),
            ent.Owner,
            args.User);
    }

    private void OnUnloadDoAfter(Entity<CMUCargoVehicleComponent> ent, ref CMUCargoVehicleUnloadDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled || !CanFinishUnload(ent, args.User, out var crate))
            return;

        _containers.Remove(crate, ent.Comp.CargoContainer!);
        var offset = _random.NextAngle().ToWorldVec() * 0.8f;
        _transform.SetCoordinates(crate, Transform(args.User).Coordinates.Offset(offset));
        _popup.PopupEntity(
            Loc.GetString("cmu-cargo-vehicle-unload-finished", ("crate", crate)),
            ent.Owner,
            args.User);
    }

    private void OnVehicleExploded(Entity<CMUCargoVehicleComponent> ent, ref CMExplosiveTriggeredEvent args)
    {
        if (TryComp(ent.Owner, out CMUCargoVehicleControlSessionComponent? session))
            EndControl((ent.Owner, session), Loc.GetString("cmu-cargo-vehicle-control-ended-vehicle-lost"));

        ScatterCargo(ent);

        var xform = Transform(ent.Owner);
        var coords = xform.Coordinates;
        var wreck = Spawn(ent.Comp.WreckPrototype, coords);
        _transform.SetLocalRotation(wreck, xform.LocalRotation);

        var debrisCount = _random.Next(ent.Comp.MinimumDebris, ent.Comp.MaximumDebris + 1);
        var firstDebris = _random.Next(ent.Comp.DebrisPrototypes.Count);
        for (var i = 0; i < debrisCount; i++)
        {
            var debrisPrototype = ent.Comp.DebrisPrototypes[(firstDebris + i) % ent.Comp.DebrisPrototypes.Count];
            var debris = Spawn(debrisPrototype, coords);
            _throwing.TryThrow(
                debris,
                _random.NextAngle().ToWorldVec(),
                baseThrowSpeed: _random.NextFloat(4f, 7f),
                doSpin: true,
                compensateFriction: true);
        }

        for (var i = 0; i < 5; i++)
        {
            var oilOffset = _random.NextAngle().ToWorldVec() * _random.NextFloat(0.25f, 2f);
            Spawn(ent.Comp.OilSpawnerPrototype, coords.Offset(oilOffset));
        }

        _rmcFlammable.SpawnFireDiamond(
            ent.Comp.FirePrototype,
            coords,
            ent.Comp.FireRange,
            duration: ent.Comp.FireDuration);
    }

    private void OnControllerShutdown(Entity<CMUCargoVehicleControllerComponent> ent, ref ComponentShutdown args)
    {
        EndControlForController(ent.Owner, Loc.GetString("cmu-cargo-vehicle-control-ended-controller-lost"));
        UnlinkVehicle(ent);
    }

    private void OnControllerTerminating(Entity<CMUCargoVehicleControllerComponent> ent, ref EntityTerminatingEvent args)
    {
        EndControlForController(ent.Owner, Loc.GetString("cmu-cargo-vehicle-control-ended-controller-lost"));
        UnlinkVehicle(ent);
    }

    private void OnControllerUnequipped(Entity<CMUCargoVehicleControllerComponent> ent, ref GotUnequippedHandEvent args)
    {
        EndControlForController(ent.Owner, Loc.GetString("cmu-cargo-vehicle-control-ended-controller-dropped"));
    }

    private void OnControllerUseInHand(Entity<CMUCargoVehicleControllerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (TryGetSessionForController(ent.Owner, out var session))
        {
            EndControl(session, Loc.GetString("cmu-cargo-vehicle-control-ended-manual"));
            return;
        }

        StartControl(ent, args.User);
    }

    private void OnReturnAction(Entity<CMUCargoVehicleControlSessionComponent> ent, ref CMUCargoVehicleReturnActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;
        EndControl(ent, Loc.GetString("cmu-cargo-vehicle-control-ended-manual"));
    }

    private void OnSelfDestructAction(
        Entity<CMUCargoVehicleControlSessionComponent> ent,
        ref CMUCargoVehicleSelfDestructActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner ||
            !TryComp(ent.Owner, out CMUCargoVehicleComponent? vehicle))
        {
            return;
        }

        args.Handled = true;
        Arm((ent.Owner, vehicle), CMUCargoVehicleArmingMode.Manual, ent.Comp.Operator);
    }

    private void OnToggleBayAction(
        Entity<CMUCargoVehicleControlSessionComponent> ent,
        ref CMUCargoVehicleToggleBayActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner ||
            !TryComp(ent.Owner, out CMUCargoVehicleComponent? vehicle))
        {
            return;
        }

        args.Handled = true;
        ToggleBay((ent.Owner, vehicle), ent.Owner);
    }

    private void OnVehiclePlayerDetached(Entity<CMUCargoVehicleControlSessionComponent> ent, ref PlayerDetachedEvent args)
    {
        EndControl(ent, Loc.GetString("cmu-cargo-vehicle-control-ended-disconnected"));
    }

    private void OnOperatorMobStateChanged(
        Entity<CMUCargoVehicleRemotePilotComponent> ent,
        ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        EndControlForOperator(ent.Owner, Loc.GetString("cmu-cargo-vehicle-control-ended-operator-disabled"));
    }

    private void OnRemotePilotShutdown(Entity<CMUCargoVehicleRemotePilotComponent> ent, ref ComponentShutdown args)
    {
        RestoreOperatorSsdIndicator(ent);
    }

    private void OnRemotePilotPlayerAttached(Entity<CMUCargoVehicleRemotePilotComponent> ent, ref PlayerAttachedEvent args)
    {
        SuppressSsdIndicator(ent.Owner);
    }

    private void OnRemotePilotPlayerDetached(Entity<CMUCargoVehicleRemotePilotComponent> ent, ref PlayerDetachedEvent args)
    {
        SuppressSsdIndicator(ent.Owner);
    }

    private void PairController(
        Entity<CMUCargoVehicleComponent> vehicle,
        Entity<CMUCargoVehicleControllerComponent> controller,
        EntityUid user)
    {
        if (vehicle.Comp.ArmingMode != CMUCargoVehicleArmingMode.None ||
            HasComp<CMUCargoVehicleControlSessionComponent>(vehicle.Owner))
        {
            Popup(user, "cmu-cargo-vehicle-pair-busy");
            return;
        }

        if (!_hands.IsHolding(user, controller.Owner))
        {
            Popup(user, "cmu-cargo-vehicle-controller-must-hold");
            return;
        }

        if (controller.Comp.LinkedVehicle is { } previous && previous != vehicle.Owner &&
            TryComp(previous, out CMUCargoVehicleComponent? previousVehicle))
        {
            if (previousVehicle.ArmingMode != CMUCargoVehicleArmingMode.None ||
                HasComp<CMUCargoVehicleControlSessionComponent>(previous))
            {
                Popup(user, "cmu-cargo-vehicle-pair-busy");
                return;
            }

            previousVehicle.Controller = null;
            Dirty(previous, previousVehicle);
        }

        if (vehicle.Comp.Controller is { } oldController && oldController != controller.Owner &&
            TryComp(oldController, out CMUCargoVehicleControllerComponent? oldControllerComp))
        {
            oldControllerComp.LinkedVehicle = null;
            Dirty(oldController, oldControllerComp);
        }

        controller.Comp.LinkedVehicle = vehicle.Owner;
        vehicle.Comp.Controller = controller.Owner;
        Dirty(controller);
        Dirty(vehicle);
        _popup.PopupEntity(
            Loc.GetString("cmu-cargo-vehicle-pair-success", ("vehicle", vehicle.Owner)),
            vehicle.Owner,
            user);
    }

    private void StartControl(Entity<CMUCargoVehicleControllerComponent> controller, EntityUid user)
    {
        if (!_hands.IsHolding(user, controller.Owner))
        {
            Popup(user, "cmu-cargo-vehicle-controller-must-hold");
            return;
        }

        if (controller.Comp.LinkedVehicle is not { } vehicleUid ||
            !TryComp(vehicleUid, out CMUCargoVehicleComponent? vehicle))
        {
            Popup(user, "cmu-cargo-vehicle-controller-unpaired");
            return;
        }

        if (vehicle.ArmingMode != CMUCargoVehicleArmingMode.None ||
            HasComp<CMUCargoVehicleControlSessionComponent>(vehicleUid))
        {
            Popup(user, "cmu-cargo-vehicle-control-busy");
            return;
        }

        if (!_mobState.IsAlive(user) ||
            !_mind.TryGetMind(user, out var mindId, out var mind) ||
            mind.OwnedEntity != user ||
            mind.VisitingEntity != null)
        {
            Popup(user, "cmu-cargo-vehicle-control-unavailable");
            return;
        }

        StopMotion(user);

        var pilot = EnsureComp<CMUCargoVehicleRemotePilotComponent>(user);
        pilot.Vehicle = vehicleUid;
        pilot.Controller = controller.Owner;
        pilot.MindId = mindId;
        RemoveOperatorSsdIndicator((user, pilot));
        Dirty(user, pilot);

        var session = EnsureComp<CMUCargoVehicleControlSessionComponent>(vehicleUid);
        session.Operator = user;
        session.Controller = controller.Owner;
        session.MindId = mindId;
        session.ReturnAction = _actions.AddAction(vehicleUid, ReturnActionId);
        session.ToggleBayAction = _actions.AddAction(vehicleUid, ToggleBayActionId);
        session.SelfDestructAction = _actions.AddAction(vehicleUid, SelfDestructActionId);
        Dirty(vehicleUid, session);

        _mind.Visit(mindId, vehicleUid, mind);
        _popup.PopupEntity(
            Loc.GetString("cmu-cargo-vehicle-control-start", ("vehicle", vehicleUid)),
            vehicleUid,
            vehicleUid);
    }

    private bool ValidateSession(Entity<CMUCargoVehicleControlSessionComponent> session, out string reason)
    {
        reason = Loc.GetString("cmu-cargo-vehicle-control-ended-link-lost");
        if (TerminatingOrDeleted(session.Owner) ||
            TerminatingOrDeleted(session.Comp.Operator) ||
            TerminatingOrDeleted(session.Comp.Controller))
        {
            return false;
        }

        if (!_hands.IsHolding(session.Comp.Operator, session.Comp.Controller))
        {
            reason = Loc.GetString("cmu-cargo-vehicle-control-ended-controller-dropped");
            return false;
        }

        if (!_mobState.IsAlive(session.Comp.Operator))
        {
            reason = Loc.GetString("cmu-cargo-vehicle-control-ended-operator-disabled");
            return false;
        }

        if (!TryComp(session.Comp.MindId, out MindComponent? mind) || mind.VisitingEntity != session.Owner)
            return false;

        return true;
    }

    private void EndControl(Entity<CMUCargoVehicleControlSessionComponent> session, string reason)
    {
        if (session.Comp.Ending || session.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        session.Comp.Ending = true;

        RemoveAction(session.Owner, ref session.Comp.ReturnAction);
        RemoveAction(session.Owner, ref session.Comp.ToggleBayAction);
        RemoveAction(session.Owner, ref session.Comp.SelfDestructAction);

        if (TryComp(session.Comp.MindId, out MindComponent? mind) && mind.VisitingEntity == session.Owner)
            _mind.UnVisit(session.Comp.MindId, mind);

        if (!TerminatingOrDeleted(session.Comp.Operator))
        {
            RemCompDeferred<CMUCargoVehicleRemotePilotComponent>(session.Comp.Operator);
            _popup.PopupEntity(reason, session.Comp.Operator, session.Comp.Operator, PopupType.SmallCaution);
        }

        StopMotion(session.Owner);
        RemCompDeferred<CMUCargoVehicleControlSessionComponent>(session.Owner);
    }

    private void Arm(
        Entity<CMUCargoVehicleComponent> vehicle,
        CMUCargoVehicleArmingMode mode,
        EntityUid? user)
    {
        if (vehicle.Comp.ArmingMode != CMUCargoVehicleArmingMode.None)
        {
            if (user is { } existingUser)
                Popup(existingUser, "cmu-cargo-vehicle-already-armed");
            return;
        }

        vehicle.Comp.ArmingMode = mode;
        Dirty(vehicle);

        if (mode == CMUCargoVehicleArmingMode.Automatic)
        {
            if (TryComp(vehicle.Owner, out CMUCargoVehicleControlSessionComponent? session))
                EndControl((vehicle.Owner, session), Loc.GetString("cmu-cargo-vehicle-control-ended-critical-damage"));

            StopMotion(vehicle.Owner);
            if (TryComp(vehicle.Owner, out VehicleComponent? vehicleComp))
                _vehicles.RefreshCanRun((vehicle.Owner, vehicleComp));
        }

        _trigger.HandleTimerTrigger(
            vehicle.Owner,
            user,
            vehicle.Comp.DetonationDelay,
            vehicle.Comp.BeepInterval,
            0f,
            vehicle.Comp.BeepSound);

        _popup.PopupEntity(
            Loc.GetString(mode == CMUCargoVehicleArmingMode.Automatic
                ? "cmu-cargo-vehicle-auto-armed"
                : "cmu-cargo-vehicle-manual-armed"),
            vehicle.Owner,
            Filter.Pvs(vehicle.Owner),
            true,
            PopupType.LargeCaution);
    }

    private void ToggleBay(Entity<CMUCargoVehicleComponent> vehicle, EntityUid user)
    {
        if (vehicle.Comp.ArmingMode != CMUCargoVehicleArmingMode.None)
        {
            Popup(user, "cmu-cargo-vehicle-cargo-armed");
            return;
        }

        vehicle.Comp.BayOpen = !vehicle.Comp.BayOpen;
        Dirty(vehicle);
        UpdateBayAppearance(vehicle);
        _audio.PlayPvs(vehicle.Comp.RampSound, vehicle.Owner);
        _popup.PopupEntity(
            Loc.GetString(vehicle.Comp.BayOpen
                ? "cmu-cargo-vehicle-bay-opened"
                : "cmu-cargo-vehicle-bay-closed"),
            vehicle.Owner,
            user);
    }

    private void RemoveOperatorSsdIndicator(Entity<CMUCargoVehicleRemotePilotComponent> ent)
    {
        if (!TryComp<SSDIndicatorComponent>(ent.Owner, out var ssd))
        {
            ent.Comp.HadSsdIndicator = false;
            return;
        }

        ent.Comp.HadSsdIndicator = true;
        ent.Comp.SsdIndicatorIcon = ssd.Icon;
        RemComp<SSDIndicatorComponent>(ent.Owner);
        _statusEffects.TryRemoveStatusEffect(ent.Owner, SSDIndicatorSystem.StatusEffectSSDSleeping);
    }

    private void RestoreOperatorSsdIndicator(Entity<CMUCargoVehicleRemotePilotComponent> ent)
    {
        if (!ent.Comp.HadSsdIndicator || TerminatingOrDeleted(ent.Owner))
            return;

        var ssd = EnsureComp<SSDIndicatorComponent>(ent.Owner);
        ssd.Icon = ent.Comp.SsdIndicatorIcon;
        SuppressSsdIndicator(ent.Owner);
    }

    private void SuppressSsdIndicator(EntityUid uid)
    {
        if (!TryComp<SSDIndicatorComponent>(uid, out var ssd))
            return;

        ssd.IsSSD = false;
        _statusEffects.TryRemoveStatusEffect(uid, SSDIndicatorSystem.StatusEffectSSDSleeping);
        Dirty(uid, ssd);
    }

    private void StartLoad(Entity<CMUCargoVehicleComponent> vehicle, EntityUid user, EntityUid crate)
    {
        if (!CanFinishLoad(vehicle, user, crate))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            vehicle.Comp.CargoDelay,
            new CMUCargoVehicleLoadDoAfterEvent(),
            vehicle.Owner,
            target: vehicle.Owner,
            used: crate)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget,
            ExtraCheck = () => CanFinishLoad(vehicle, user, crate),
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-cargo-vehicle-load-start"), vehicle.Owner, user);
    }

    private void StartUnload(Entity<CMUCargoVehicleComponent> vehicle, EntityUid user)
    {
        if (!CanFinishUnload(vehicle, user, out _))
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            vehicle.Comp.CargoDelay,
            new CMUCargoVehicleUnloadDoAfterEvent(),
            vehicle.Owner,
            target: vehicle.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget,
            ExtraCheck = () => CanFinishUnload(vehicle, user, out _),
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-cargo-vehicle-unload-start"), vehicle.Owner, user);
    }

    private bool CanFinishLoad(Entity<CMUCargoVehicleComponent> vehicle, EntityUid user, EntityUid crate)
    {
        return CanHandleCargo(vehicle) &&
               vehicle.Comp.CargoContainer is { ContainedEntity: null } cargoContainer &&
               !TerminatingOrDeleted(crate) &&
               HasComp<EntityStorageComponent>(crate) &&
               HasComp<PowerLoaderGrabbableComponent>(crate) &&
               TryComp(user, out PullerComponent? puller) &&
               puller.Pulling == crate &&
               IsNear(user, vehicle.Owner) &&
               IsNear(crate, vehicle.Owner) &&
               _containers.CanInsert(crate, cargoContainer);
    }

    private bool CanFinishUnload(
        Entity<CMUCargoVehicleComponent> vehicle,
        EntityUid user,
        out EntityUid crate)
    {
        crate = default;
        if (!CanHandleCargo(vehicle) ||
            vehicle.Comp.CargoContainer?.ContainedEntity is not { } contained ||
            !IsNear(user, vehicle.Owner))
        {
            return false;
        }

        crate = contained;
        return true;
    }

    private bool CanHandleCargo(Entity<CMUCargoVehicleComponent> vehicle)
    {
        if (!vehicle.Comp.BayOpen || vehicle.Comp.ArmingMode != CMUCargoVehicleArmingMode.None)
            return false;

        return !TryComp(vehicle.Owner, out GridVehicleMoverComponent? mover) ||
               (!mover.IsMoving &&
                MathF.Abs(mover.CurrentSpeed) < 0.01f &&
                MathF.Abs(mover.AngularVelocityDegrees) < 0.01f);
    }

    private bool TryGetPulledCrate(EntityUid user, EntityUid vehicle, out EntityUid crate)
    {
        crate = default;
        if (!TryComp(user, out PullerComponent? puller) || puller.Pulling is not { } pulled ||
            !HasComp<EntityStorageComponent>(pulled) ||
            !HasComp<PowerLoaderGrabbableComponent>(pulled) ||
            !IsNear(pulled, vehicle))
        {
            return false;
        }

        crate = pulled;
        return true;
    }

    private void ScatterCargo(Entity<CMUCargoVehicleComponent> vehicle)
    {
        if (vehicle.Comp.CargoContainer?.ContainedEntity is not { } crate)
            return;

        _containers.Remove(crate, vehicle.Comp.CargoContainer);
        var coords = Transform(vehicle.Owner).Coordinates;

        if (TryComp(crate, out EntityStorageComponent? storage))
        {
            foreach (var item in storage.Contents.ContainedEntities.ToArray())
            {
                _containers.Remove(item, storage.Contents);
                _transform.SetCoordinates(item, coords);
                _throwing.TryThrow(
                    item,
                    _random.NextAngle().ToWorldVec(),
                    baseThrowSpeed: _random.NextFloat(3f, 7f),
                    doSpin: true,
                    compensateFriction: true);
            }
        }

        QueueDel(crate);
    }

    private bool IsNear(EntityUid first, EntityUid second)
    {
        return Transform(first).Coordinates.TryDistance(EntityManager, Transform(second).Coordinates, out var distance) &&
               distance <= 2.5f;
    }

    private void UpdateBayAppearance(Entity<CMUCargoVehicleComponent> vehicle)
    {
        _appearance.SetData(vehicle.Owner, CMUCargoVehicleVisuals.BayOpen, vehicle.Comp.BayOpen);
    }

    private void RemoveAction(EntityUid owner, ref EntityUid? action)
    {
        if (action is not { } actionUid)
            return;

        if (!TerminatingOrDeleted(owner))
            _actions.RemoveAction(owner, actionUid);
        action = null;
    }

    private void StopMotion(EntityUid uid)
    {
        if (!TryComp(uid, out PhysicsComponent? physics))
            return;

        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.SetAngularVelocity(uid, 0f, body: physics);
        _physics.SetBodyStatus(uid, physics, BodyStatus.OnGround);
    }

    private bool TryGetSessionForController(
        EntityUid controller,
        out Entity<CMUCargoVehicleControlSessionComponent> session)
    {
        var query = EntityQueryEnumerator<CMUCargoVehicleControlSessionComponent>();
        while (query.MoveNext(out var vehicle, out var control))
        {
            if (control.Controller != controller)
                continue;

            session = (vehicle, control);
            return true;
        }

        session = default;
        return false;
    }

    private void EndControlForController(EntityUid controller, string reason)
    {
        if (TryGetSessionForController(controller, out var session))
            EndControl(session, reason);
    }

    private void EndControlForOperator(EntityUid user, string reason)
    {
        if (!TryComp(user, out CMUCargoVehicleRemotePilotComponent? pilot) ||
            !TryComp(pilot.Vehicle, out CMUCargoVehicleControlSessionComponent? session))
        {
            return;
        }

        EndControl((pilot.Vehicle, session), reason);
    }

    private void UnlinkController(Entity<CMUCargoVehicleComponent> vehicle)
    {
        if (vehicle.Comp.Controller is { } controller &&
            TryComp(controller, out CMUCargoVehicleControllerComponent? controllerComp) &&
            controllerComp.LinkedVehicle == vehicle.Owner)
        {
            controllerComp.LinkedVehicle = null;
            Dirty(controller, controllerComp);
        }

        vehicle.Comp.Controller = null;
    }

    private void UnlinkVehicle(Entity<CMUCargoVehicleControllerComponent> controller)
    {
        if (controller.Comp.LinkedVehicle is { } vehicle &&
            TryComp(vehicle, out CMUCargoVehicleComponent? vehicleComp) &&
            vehicleComp.Controller == controller.Owner)
        {
            vehicleComp.Controller = null;
            Dirty(vehicle, vehicleComp);
        }

        controller.Comp.LinkedVehicle = null;
    }

    private void Popup(EntityUid user, string locId)
    {
        _popup.PopupEntity(Loc.GetString(locId), user, user, PopupType.SmallCaution);
    }
}
