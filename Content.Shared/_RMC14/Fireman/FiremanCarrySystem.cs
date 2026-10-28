using Content.Shared.CMU14.Threats.Mobs.Biomorph; // CMU14
using System.Numerics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Humanoid; // CMU14: HumanoidProfileComponent for the Yautja instant-carry gate
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Strip;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Components; // CMU14
using Robust.Shared.Physics.Systems; // CMU14
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.CMU14.Yautja; // CMU14: YautjaComponent / YautjaBadBloodComponent for the instant-carry gate

namespace Content.Shared._RMC14.Fireman;

public sealed partial class FiremanCarrySystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!; // CMU14
    [Dependency] private RMCPullingSystem _rmcPulling = default!;
    [Dependency] private SharedRMCSpriteSystem _rmcSprite = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private readonly List<(EntityUid Target, EntityUid Carrier)> _toReparent = new();

    private EntityQuery<HandsComponent> _handsQuery;

    public override void Initialize()
    {
        _handsQuery = GetEntityQuery<HandsComponent>();

        SubscribeLocalEvent<FiremanCarriableComponent, CanDragEvent>(OnCarriableCanDrag);
        SubscribeLocalEvent<FiremanCarriableComponent, CanDropDraggedEvent>(OnCarriableCanDropDragged);
        SubscribeLocalEvent<FiremanCarriableComponent, DragDropDraggedEvent>(OnCarriableDragDropDragged, before: [typeof(SharedStrippableSystem)]);
        SubscribeLocalEvent<FiremanCarriableComponent, DoAfterAttemptEvent<FiremanCarryDoAfterEvent>>(OnCarriableFiremanCarryDoAfterAttempt);
        SubscribeLocalEvent<FiremanCarriableComponent, FiremanCarryDoAfterEvent>(OnCarriableFiremanCarryDoAfter);
        SubscribeLocalEvent<FiremanCarriableComponent, StandAttemptEvent>(OnCarriableStandAttempt);
        SubscribeLocalEvent<FiremanCarriableComponent, UpdateCanMoveEvent>(OnCarriableCanMove);
        SubscribeLocalEvent<FiremanCarriableComponent, MoveInputEvent>(OnCarriableMoveInput);
        SubscribeLocalEvent<FiremanCarriableComponent, BreakFiremanCarryDoAfterEvent>(OnCarriableBreakCarryDoAfter);
        SubscribeLocalEvent<FiremanCarriableComponent, PullStartedMessage>(OnCarriablePullStarted);
        SubscribeLocalEvent<FiremanCarriableComponent, PullStoppedMessage>(OnCarriablePullStopped);
        SubscribeLocalEvent<FiremanCarriableComponent, PullAttemptEvent>(OnCarriablePullAttempt);

        SubscribeLocalEvent<CanFiremanCarryComponent, CanDropTargetEvent>(OnCarrierCanDropTarget);
        SubscribeLocalEvent<CanFiremanCarryComponent, PullStartedMessage>(OnCarrierPullStarted);
        SubscribeLocalEvent<CanFiremanCarryComponent, PullStoppedMessage>(OnCarrierPullStopped);
        SubscribeLocalEvent<CanFiremanCarryComponent, PullSlowdownAttemptEvent>(OnCarrierPullSlowdownAttempt);
        SubscribeLocalEvent<CanFiremanCarryComponent, MobStateChangedEvent>(OnCarrierMobStateChanged);
        SubscribeLocalEvent<CanFiremanCarryComponent, RMCPullToggleEvent>(OnCarrierPullToggle);
        SubscribeLocalEvent<CanFiremanCarryComponent, BeforeThrowEvent>(OnCarrierBeforeThrow);

        SubscribeLocalEvent<BeingFiremanCarriedComponent, PreventCollideEvent>(OnBeingCarriedPreventCollide);
    }

    private void OnCarriableCanDrag(Entity<FiremanCarriableComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnCarriableCanDropDragged(Entity<FiremanCarriableComponent> ent, ref CanDropDraggedEvent args)
    {
        if (args.User != args.Target || !TryComp(args.Target, out CanFiremanCarryComponent? carrier))
            return;

        if (_rmcPulling.IsPulling(args.User, ent.Owner))
        {
            args.CanDrop = true;
            args.Handled = true;
        }
    }

    private void OnCarrierCanDropTarget(Entity<CanFiremanCarryComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.User != ent.Owner || !TryComp(args.Dragged, out FiremanCarriableComponent? carriable))
            return;

        if (_rmcPulling.IsPulling(args.User, args.Dragged))
        {
            args.CanDrop = true;
            args.Handled = true;
        }
    }

    private void OnCarrierBeforeThrow(Entity<CanFiremanCarryComponent> carrier, ref BeforeThrowEvent args)
    {
        if (args.Cancelled)
            return;

        var actualItem = GetActualItem(args.ItemUid);
        if (!TryComp(actualItem, out FiremanCarriableComponent? carriable))
            return;

        if (!ValidateThrow(carrier, actualItem, carriable, args.ItemUid))
        {
            args.Cancelled = true;
            return;
        }

        args.ItemUid = actualItem;

        // CMU14: a throw ends the carry. Without this the victim kept BeingCarried and stayed parented to the
        // thrower after being flung, so it could not be picked up again. StopCarry clears the carry state, restores
        // standing, unparents on the next tick and deletes the virtual item that represented the victim in hand.
        StopCarry(carrier.AsNullable(), (actualItem, carriable));
    }

    private EntityUid GetActualItem(EntityUid itemUid)
    {
        return TryComp(itemUid, out VirtualItemComponent? virtualItem) ? virtualItem.BlockingEntity : itemUid;
    }

    private bool ValidateThrow(Entity<CanFiremanCarryComponent> carrier, EntityUid target, FiremanCarriableComponent carriable, EntityUid heldItem)
    {
        if (!carriable.BeingCarried)
        {
            _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-need-carry-first"), carrier, carrier, PopupType.SmallCaution);
            return false;
        }

        if (!carriable.CanThrow)
        {
            _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-cant-throw"), carrier, carrier, PopupType.SmallCaution);
            return false;
        }

        if (!ValidateCarrierWhitelist(carrier, target, carriable))
        {
            _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-cant-throw"), carrier, carrier, PopupType.SmallCaution);
            return false;
        }

        return _handsQuery.TryComp(carrier, out var hands) && _hands.IsHolding((carrier.Owner, hands), heldItem);
    }

    private void OnCarriableDragDropDragged(Entity<FiremanCarriableComponent> ent, ref DragDropDraggedEvent args)
    {
        var user = args.User;
        if (!TryComp(user, out CanFiremanCarryComponent? carrier) || args.Target != user)
            return;

        // CMU14: a Yautja lifting an eligible human or xenomorph only needs the aggressive grab. CM-SS13's GRAB_CARRY
        // holds the victim directly with no separate pull object, and demanding a live pull here made the carry fail
        // whenever the pull had already dropped (walking, being stunned), which is why pickups felt intermittent.
        // CMU14: the upstream line was removed outright; kept here so a downmerge flags the divergence:
        // if (!_rmcPulling.IsPulling(user, ent.Owner))
        //     return;
        var yautjaInstant = HasComp<YautjaComponent>(user) &&
                            (HasComp<XenoComponent>(ent.Owner) ||
                             HasComp<HumanoidProfileComponent>(ent.Owner) ||
                             HasComp<BiomorphComponent>(ent.Owner));
        if (!yautjaInstant && !_rmcPulling.IsPulling(user, ent.Owner))
            return;

        // CMU14: a Yautja dragging a human themselves without an aggressive grab is stripping them, not
        // carrying them. This handler runs before SharedStrippableSystem, so claiming the event there would swallow
        // the strip and replace it with the "grab them aggressively" popup - leave it untouched instead.
        if (yautjaInstant && !carrier.AggressiveGrab)
            return;

        args.Handled = true;

        if (!CanCarryEntity(user, ent.Owner, carrier, ent.Comp))
            return;

        if (!carrier.AggressiveGrab)
        {
            _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-need-aggressive-grab"), ent, user, PopupType.MediumCaution);
            return;
        }

        // CMU14: a Yautja lifts an eligible human or xenomorph outright - CM-SS13's grab carries in one motion, so
        // there is no hold-still timer. Every other carrier, and every other target type, keeps the normal do-after.
        if (yautjaInstant)
        {
            StartCarry((ent.Owner, ent.Comp), user, carrier);
            return;
        }

        var delay = ent.Comp.Delay * _skills.GetSkillDelayMultiplier(user, ent.Comp.Skill);
        var ev = new FiremanCarryDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, ent, ent, user)
        {
            BreakOnMove = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            ForceVisible = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var target = Identity.Name(ent, EntityManager, args.User);
        _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-start-loading", ("targetName", target)), ent, user, PopupType.Medium);
    }

    private bool CanCarryEntity(EntityUid user, EntityUid target, CanFiremanCarryComponent carrier, FiremanCarriableComponent carriable)
    {
        if (!ValidateCarrierWhitelist(user, target, carriable))
            return false;

        if (carriable.CarrierWhitelist == null &&
            !HasComp<ChemicalMuscleStimulationComponent>(user) &&
            !_skills.HasSkill(user, carriable.Skill, 1))
        {
            _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-not-trained"), target, user, PopupType.MediumCaution);
            return false;
        }

        return true;
    }

    private bool ValidateCarrierWhitelist(EntityUid carrier, EntityUid target, FiremanCarriableComponent carriable)
    {
        return carriable.CarrierWhitelist == null || _whitelist.IsValid(carriable.CarrierWhitelist, carrier);
    }

    private void OnCarriableFiremanCarryDoAfterAttempt(Entity<FiremanCarriableComponent> ent, ref DoAfterAttemptEvent<FiremanCarryDoAfterEvent> args)
    {
        if (!_rmcPulling.IsPulling(args.DoAfter.Args.User, ent.Owner))
            args.Cancel();
    }

    private void OnCarriableFiremanCarryDoAfter(Entity<FiremanCarriableComponent> ent, ref FiremanCarryDoAfterEvent args)
    {
        var user = args.User;
        if (args.Cancelled || args.Handled)
            return;

        if (!TryComp(user, out CanFiremanCarryComponent? carrier))
            return;

        if (_transform.IsParentOf(Transform(ent.Owner), user))
            return;

        // CMU14: the carry body used to be inline here; it is now the shared StartCarry so the instant Yautja path
        // runs exactly the same initialisation.
        StartCarry((ent.Owner, ent.Comp), user, carrier);
        args.Handled = true; // CMU14: moved here from the carry body (shared with the instant Yautja path).
    }

    // CMU14: the carry itself, split out of the do-after handler so a Yautja can lift an eligible human or xeno
    // outright and both paths share the same initialisation.
    // CMU14 method
    private void StartCarry(Entity<FiremanCarriableComponent> ent, EntityUid user, CanFiremanCarryComponent carrier)
    {
        if (_transform.IsParentOf(Transform(ent.Owner), user))
            return;


        ent.Comp.BeingCarried = true;
        Dirty(ent);

        EnsureComp<BeingFiremanCarriedComponent>(ent);

        carrier.Carrying = ent;
        Dirty(user, carrier);

        if (!_timing.ApplyingState && !HasComp<MouseRotatorComponent>(user))
            RemCompDeferred<NoRotateOnMoveComponent>(user);

        // CMU14: upstream set `args.Handled = true;` here. It moved to the do-after caller because StartCarry is
        // shared with the instant Yautja path, which has no do-after to mark handled.
        // args.Handled = true;

        _transform.SetParent(ent, user);
        _transform.SetLocalPosition(ent, Vector2.Zero);

        // CMU14: parenting preserves world momentum, but a carried body must be stationary relative to its carrier.
        if (TryComp(ent, out PhysicsComponent? physics))
            _physics.ResetDynamics(ent, physics);

        _standing.Down(ent, changeCollision: true);

        // CMU14: match the aggressive-grab state. OnAggressiveGrab gives a xenomorph KnockedDownComponent,
        // only calling Down() left a carried xeno with its normal standing sprite, so it looked like an ordinary xeno
        // glued onto the carrier.
        if (HasComp<XenoComponent>(ent))
            EnsureComp<KnockedDownComponent>(ent);

        _movementSpeed.RefreshMovementSpeedModifiers(user);
        _rmcSprite.SetRenderOrder(user, 1);

        // CMU14: the carried mob has to occupy one of the carrier's hands through a virtual item. ValidateThrow
        // requires the carrier to be holding the carried entity before it can be thrown, and StopCarry deletes the
        // matching virtual item on drop, so without this a carried mob could be picked up but never thrown. Spawned
        // here so the instant Yautja path and the delayed path share exactly the same initialisation.
        _virtualItem.TrySpawnVirtualItemInHand(ent.Owner, user, dropOthers: false, silent: true);

    }

    private void OnCarriableStandAttempt(Entity<FiremanCarriableComponent> ent, ref StandAttemptEvent args)
    {
        if (ent.Comp.BeingCarried || IsBeingAggressivelyGrabbed(ent))
            args.Cancel();
    }

    private void OnCarriableCanMove(Entity<FiremanCarriableComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (IsBeingAggressivelyGrabbed(ent))
            args.Cancel();
    }

    private void OnCarriableMoveInput(Entity<FiremanCarriableComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement || ent.Comp.BreakingFree)
            return;

        if (!ent.Comp.BeingCarried && !IsBeingAggressivelyGrabbed(ent))
            return;

        var ev = new BreakFiremanCarryDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.Delay, ev, ent);
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        ent.Comp.BreakingFree = true;
        if (!_rmcPulling.IsBeingPulled(ent.Owner, out var puller))
            return;

        var selfMsg = Loc.GetString("rmc-pull-break-start-self", ("puller", puller));
        _popup.PopupEntity(selfMsg, ent, ent, PopupType.MediumCaution);

        var others = Filter.PvsExcept(ent, entityManager: EntityManager);
        foreach (var other in others.Recipients)
        {
            if (other.AttachedEntity is not { } recipient)
                continue;

            var pullerName = Identity.Name(puller, EntityManager, recipient);
            var pulledName = Identity.Name(ent, EntityManager, recipient);
            var msg = Loc.GetString("rmc-pull-break-start-others", ("puller", pullerName), ("pulled", pulledName));
            _popup.PopupEntity(msg, ent, recipient, PopupType.MediumCaution);
        }
    }

    private void OnCarriableBreakCarryDoAfter(Entity<FiremanCarriableComponent> ent, ref BreakFiremanCarryDoAfterEvent args)
    {
        ent.Comp.BreakingFree = false;
        Dirty(ent);

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        ent.Comp.BeingCarried = false;
        RemCompDeferred<BeingFiremanCarriedComponent>(ent);

        if (!_rmcPulling.IsBeingPulled(ent.Owner, out var puller))
            return;

        StopCarry(puller, (ent, ent));
        var selfMsg = Loc.GetString("rmc-pull-break-finish-self", ("puller", puller));
        _popup.PopupEntity(selfMsg, ent, ent, PopupType.MediumCaution);

        var others = Filter.PvsExcept(ent, entityManager: EntityManager);
        foreach (var other in others.Recipients)
        {
            if (other.AttachedEntity is not { } recipient)
                continue;

            var pullerName = Identity.Name(puller, EntityManager, recipient);
            var pulledName = Identity.Name(ent, EntityManager, recipient);
            var msg = Loc.GetString("rmc-pull-break-finish-others", ("puller", pullerName), ("pulled", pulledName));
            _popup.PopupEntity(msg, ent, recipient, PopupType.MediumCaution);
        }
    }

    private void OnCarriablePullStarted(Entity<FiremanCarriableComponent> ent, ref PullStartedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        if (_rmcPulling.IsBeingPulled(ent.Owner, out var puller) &&
            TryComp(puller, out CanFiremanCarryComponent? carrier))
        {
            StopPull((puller, carrier), ent);
        }
    }

    private void OnCarriablePullStopped(Entity<FiremanCarriableComponent> ent, ref PullStoppedMessage args)
    {
        if (ent.Owner != args.PulledUid)
            return;

        _standing.Stand(ent);
    }

    private void OnCarriablePullAttempt(Entity<FiremanCarriableComponent> ent, ref PullAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.BeingCarried || IsBeingAggressivelyGrabbed(ent))
            args.Cancelled = true;
    }

    private void OnCarrierPullStarted(Entity<CanFiremanCarryComponent> ent, ref PullStartedMessage args)
    {
        if (ent.Owner == args.PullerUid)
            StopPull(ent, args.PulledUid);
    }

    private void OnCarrierPullStopped(Entity<CanFiremanCarryComponent> ent, ref PullStoppedMessage args)
    {
        if (ent.Owner == args.PullerUid)
            StopPull(ent, args.PulledUid);
    }

    private void OnCarrierPullSlowdownAttempt(Entity<CanFiremanCarryComponent> ent, ref PullSlowdownAttemptEvent args)
    {
        if (ent.Comp.Carrying == args.Target)
            args.Cancelled = true;
    }

    private void OnCarrierMobStateChanged(Entity<CanFiremanCarryComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Comp.Carrying is not { } carrying)
            return;

        if (args.NewMobState == MobState.Dead)
            StopCarry((ent, ent), carrying);
    }

    private void OnCarrierPullToggle(Entity<CanFiremanCarryComponent> ent, ref RMCPullToggleEvent args)
    {
        args.Handled = true;

        var grabDelay = ent.Comp.AggressiveGrabDelay * _skills.GetSkillDelayMultiplier(ent.Owner, ent.Comp.Skill);
        if (ent.Comp.AggressiveGrab ||
            _timing.CurTime < ent.Comp.PullTime + grabDelay)
        {
            return;
        }

        if (!TryComp(ent, out PullerComponent? puller) ||
            puller.Pulling is not { } pulling)
        {
            return;
        }

        if (!ValidateAggressiveGrab(ent, pulling))
            return;

        ent.Comp.AggressiveGrab = true;
        Dirty(ent);

        _actionBlocker.UpdateCanMove(pulling);
        _standing.Down(pulling, changeCollision: true);

        if (HasComp<XenoComponent>(pulling))
            EnsureComp<KnockedDownComponent>(pulling);

        _rmcPulling.PlayPullEffect(ent, pulling);

        var selfMsg = Loc.GetString("rmc-pull-aggressive-self", ("pulled", pulling));
        _popup.PopupEntity(selfMsg, pulling, ent, PopupType.SmallCaution);

        var others = Filter.PvsExcept(ent, entityManager: EntityManager);
        foreach (var other in others.Recipients)
        {
            if (other.AttachedEntity is not { } recipient)
                continue;

            var pullerName = Identity.Name(ent, EntityManager, recipient);
            var pulledName = Identity.Name(pulling, EntityManager, recipient);
            var msg = Loc.GetString("rmc-pull-aggressive-others", ("puller", pullerName), ("pulled", pulledName));
            _popup.PopupEntity(msg, ent, recipient, PopupType.SmallCaution);
        }
    }

    private bool ValidateAggressiveGrab(Entity<CanFiremanCarryComponent> carrier, EntityUid target)
    {
        if (HasComp<XenoComponent>(target))
        {
            if (!TryComp(target, out FiremanCarriableComponent? carriable) ||
                carriable.CarrierWhitelist == null ||
                !_whitelist.IsValid(carriable.CarrierWhitelist, carrier.Owner))
            {
                _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-cant-grab-aggressively"), target, carrier, PopupType.MediumCaution);
                return false;
            }
        }
        else if (TryComp(target, out FiremanCarriableComponent? carriable))
        {
            if (carriable.CarrierWhitelist != null && !_whitelist.IsValid(carriable.CarrierWhitelist, carrier.Owner))
            {
                _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-cant-grab-aggressively"), target, carrier, PopupType.MediumCaution);
                return false;
            }

            if (carriable.CarrierWhitelist == null && !_skills.HasSkill(carrier.Owner, carriable.Skill, 1))
            {
                _popup.PopupEntity(Loc.GetString("rmc-fireman-carry-not-trained-grab"), target, carrier, PopupType.MediumCaution);
                return false;
            }
        }

        return true;
    }

    private void OnBeingCarriedPreventCollide(Entity<BeingFiremanCarriedComponent> ent, ref PreventCollideEvent args)
    {
        args.Cancelled = true;
    }

    private void StopPull(Entity<CanFiremanCarryComponent> ent, EntityUid target)
    {
        if (_timing.ApplyingState)
            return;

        StopCarry((ent, ent), target);

        if (HasComp<XenoComponent>(target))
            RemCompDeferred<KnockedDownComponent>(target);

        _actionBlocker.UpdateCanMove(target);

        ent.Comp.PullTime = _timing.CurTime;
        Dirty(ent);
    }

    private void StopCarry(Entity<CanFiremanCarryComponent?> user, Entity<FiremanCarriableComponent?>? targetNullable)
    {
        EntityUid? carrying = null;
        if (Resolve(user, ref user.Comp, false))
        {
            carrying = user.Comp.Carrying;
            user.Comp.Carrying = null;
            user.Comp.AggressiveGrab = false;
            Dirty(user);

            _rmcSprite.SetRenderOrder(user, 0);

            if (carrying != null)
                _virtualItem.DeleteInHandsMatching(user, carrying.Value);
        }

        if (targetNullable is not { } target)
            return;

        if (Resolve(target, ref target.Comp, false))
        {
            target.Comp.BeingCarried = false;
            Dirty(target);

            RemCompDeferred<BeingFiremanCarriedComponent>(target);

            if (carrying == target)
            {
                // CMU14: discard residual carry motion before detaching, without cancelling an intentional throw.
                if (!HasComp<ThrownItemComponent>(target) && TryComp(target, out PhysicsComponent? physics))
                    _physics.ResetDynamics(target, physics);

                _toReparent.Add((target, user));
            }
        }

        // Why: only xenos get the carry's knockdown, so a carried-and-released marine must keep theirs.
        if (HasComp<XenoComponent>(target))
            RemCompDeferred<KnockedDownComponent>(target);

        _standing.Stand(target);
        _actionBlocker.UpdateCanMove(target);
    }

    private bool IsBeingAggressivelyGrabbed(EntityUid target)
    {
        return _rmcPulling.IsBeingPulled(target, out var user) &&
               TryComp(user, out CanFiremanCarryComponent? carrier) &&
               carrier.AggressiveGrab;
    }

    public override void Update(float frameTime)
    {
        try
        {
            foreach (var (target, _) in _toReparent)
            {
                if (TerminatingOrDeleted(target))
                    continue;

                var coordinates = _transform.GetMoverCoordinates(target);
                if (target == coordinates.EntityId || TerminatingOrDeleted(coordinates.EntityId))
                    continue;

                _transform.SetCoordinates(target, coordinates);
            }
        }
        finally
        {
            _toReparent.Clear();
        }
    }
}
