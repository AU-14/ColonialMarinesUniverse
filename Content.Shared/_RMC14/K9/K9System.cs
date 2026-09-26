using System;
using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.K9.Components;
using Content.Shared._RMC14.K9.Events;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Pulling.Events;
using Content.Shared.Stunnable;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.K9;

public sealed partial class K9System : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;

    public override void Initialize()
    {
        base.Initialize();

        // K9 Dog lifecycle
        SubscribeLocalEvent<K9DogComponent, MapInitEvent>(OnDogMapInit);
        SubscribeLocalEvent<K9DogComponent, ComponentShutdown>(OnDogShutdown);

        // K9 Handler lifecycle
        SubscribeLocalEvent<K9HandlerComponent, MapInitEvent>(OnHandlerMapInit);
        SubscribeLocalEvent<K9HandlerComponent, ComponentShutdown>(OnHandlerShutdown);

        // Movement & Pulling Modifiers
        SubscribeLocalEvent<K9GrabbedComponent, RefreshMovementSpeedModifiersEvent>(OnGrabbedRefreshSpeed);
        SubscribeLocalEvent<K9ProtectorRageComponent, RefreshMovementSpeedModifiersEvent>(OnRageRefreshSpeed);
        SubscribeLocalEvent<K9DogComponent, RefreshMovementSpeedModifiersEvent>(OnDogRefreshSpeed);
        SubscribeLocalEvent<K9DogComponent, PullSlowdownAttemptEvent>(OnDogPullSlowdownAttempt);

        // Arm Grab
        SubscribeLocalEvent<K9DogComponent, K9ArmGrabActionEvent>(OnArmGrabAction);
        SubscribeLocalEvent<K9GrabbedComponent, MoveInputEvent>(OnGrabbedMoveInput);
        SubscribeLocalEvent<K9GrabbedComponent, K9EscapeGrabDoAfterEvent>(OnEscapeDoAfter);
        SubscribeLocalEvent<K9GrabbedComponent, AttemptStopPullingEvent>(OnGrabbedAttemptStopPulling);
        SubscribeLocalEvent<K9GrabbedComponent, PullStoppedMessage>(OnGrabbedPullStopped);
        SubscribeLocalEvent<K9DogComponent, DamageChangedEvent>(OnDogDamageChanged);
        SubscribeLocalEvent<K9DogComponent, KnockedDownEvent>(OnDogKnockedDown);
        SubscribeLocalEvent<K9DogComponent, StunnedEvent>(OnDogStunned);

        // Track & Request Master
        SubscribeLocalEvent<K9DogComponent, K9TrackMasterActionEvent>(OnTrackMasterAction);
        SubscribeLocalEvent<K9DogComponent, K9RequestMasterActionEvent>(OnRequestMasterAction);
        SubscribeLocalEvent<K9DogComponent, K9TameConfirmEvent>(OnTameConfirmed);
        // Confirm is RaiseLocalEvent'd on the marine (dialog owner), not broadcast.
        SubscribeLocalEvent<MarineComponent, K9MasterRequestConfirmEvent>(OnMasterRequestConfirmed);

        // Bodyguard protocol triggers on handler
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<K9HandlerComponent, KnockedDownEvent>(OnHandlerKnockedDown);
        SubscribeLocalEvent<K9HandlerComponent, StunnedEvent>(OnHandlerStunned);
        SubscribeLocalEvent<K9HandlerComponent, PullStartedMessage>(OnHandlerPullStarted);

        // Handler Actions
        SubscribeLocalEvent<K9HandlerComponent, K9TameActionEvent>(OnTameAction);
        SubscribeLocalEvent<K9HandlerComponent, K9SicEmActionEvent>(OnSicEmAction);
        SubscribeLocalEvent<K9HandlerComponent, K9EvacuateActionEvent>(OnEvacuateAction);
        SubscribeLocalEvent<K9HandlerComponent, K9GoodBoyActionEvent>(OnGoodBoyAction);

        // Battery / Power Cell interaction on dog
        SubscribeLocalEvent<K9DogComponent, InteractUsingEvent>(OnDogInteractUsing);
        SubscribeLocalEvent<K9DogComponent, MeleeHitEvent>(OnDogMeleeHit);

        // Marked Target Damage Bonus
        SubscribeLocalEvent<K9MarkedTargetComponent, DamageModifyEvent>(OnMarkedTargetDamageModify);

        // K9 Additional Access Provider (so doors and access readers find dog's AccessComponent directly)
        SubscribeLocalEvent<K9DogComponent, GetAdditionalAccessEvent>(OnDogGetAdditionalAccess);
    }

    private void OnDogGetAdditionalAccess(Entity<K9DogComponent> ent, ref GetAdditionalAccessEvent args)
    {
        args.Entities.Add(ent.Owner);
        if (ent.Comp.Master is { } master && Exists(master))
            args.Entities.Add(master);
    }

    #region Lifecycle

    private void OnDogMapInit(Entity<K9DogComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ArmGrabAction, ent.Comp.ArmGrabActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.TrackMasterAction, ent.Comp.TrackMasterActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.RequestMasterAction, ent.Comp.RequestMasterActionId);
    }

    private void OnDogShutdown(Entity<K9DogComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ArmGrabAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.TrackMasterAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.RequestMasterAction);

        if (ent.Comp.GrabbedTarget is { } target && TryComp<K9GrabbedComponent>(target, out var grabbed))
        {
            ReleaseGrab(target, grabbed);
        }

        UnbindMaster(ent.Owner, ent.Comp);
    }

    private void OnHandlerMapInit(Entity<K9HandlerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.TameAction, ent.Comp.TameActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.SicEmAction, ent.Comp.SicEmActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.EvacuateAction, ent.Comp.EvacuateActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.GoodBoyAction, ent.Comp.GoodBoyActionId);
    }

    private void OnHandlerShutdown(Entity<K9HandlerComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.TameAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.SicEmAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.EvacuateAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.GoodBoyAction);

        foreach (var dog in ent.Comp.Dogs.ToArray())
        {
            if (Exists(dog) && TryComp<K9DogComponent>(dog, out var dogComp) && dogComp.Master == ent.Owner)
                UnbindMaster(dog, dogComp);
        }
    }

    #endregion

    #region Movement Modifiers

    private void OnGrabbedRefreshSpeed(Entity<K9GrabbedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    private void OnRageRefreshSpeed(Entity<K9ProtectorRageComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    private void OnDogRefreshSpeed(Entity<K9DogComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.HandlerBonded &&
            TryComp<PullerComponent>(ent, out var puller) &&
            puller.Pulling is { } pulled &&
            TryComp<K9EvacuateTargetComponent>(pulled, out var evac) &&
            evac.Handler == ent.Comp.Master)
        {
            // Speed boost while dragging an evacuated wounded ally
            args.ModifySpeed(1.25f, 1.25f);
        }
    }

    private void OnDogPullSlowdownAttempt(Entity<K9DogComponent> ent, ref PullSlowdownAttemptEvent args)
    {
        if (ent.Comp.HandlerBonded &&
            TryComp<K9EvacuateTargetComponent>(args.Target, out var evac) &&
            evac.Handler == ent.Comp.Master)
        {
            args.Cancelled = true;
        }
    }

    #endregion

    #region Arm Grab

    private void OnArmGrabAction(Entity<K9DogComponent> ent, ref K9ArmGrabActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!target.IsValid() || target == ent.Owner)
            return;

        if (HasComp<XenoComponent>(target))
        {
            args.Handled = true;
            _popup.PopupClient(Loc.GetString("rmc-k9-arm-grab-xeno"), ent.Owner, ent.Owner);
            return;
        }

        if (!TryComp<TransformComponent>(target, out var targetXform) ||
            !TryComp<TransformComponent>(ent, out var dogXform))
            return;

        if (targetXform.MapID != dogXform.MapID ||
            Vector2.Distance(_transform.GetWorldPosition(targetXform), _transform.GetWorldPosition(dogXform)) > 2.5f)
        {
            _popup.PopupClient(Loc.GetString("rmc-k9-arm-grab-out-of-range"), ent.Owner, ent.Owner);
            return;
        }

        args.Handled = true;

        // Second use on same target: trip/knock down!
        if (TryComp<K9GrabbedComponent>(target, out var existingGrab) && existingGrab.Dog == ent.Owner)
        {
            if (_net.IsClient)
                return;

            ReleaseGrab(target, existingGrab);
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(3), true);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Weapons/alien_knockdown.ogg"), target);

            _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-trip-self", ("target", target)), ent.Owner, ent.Owner, PopupType.Medium);
            _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-trip-target", ("dog", ent.Owner)), target, target, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-trip-others", ("dog", ent.Owner), ("target", target)), ent.Owner, FilterOthers(ent.Owner, target), true, PopupType.SmallCaution);
            return;
        }

        // First use: grab and hold
        if (_net.IsClient)
            return;

        if (ent.Comp.GrabbedTarget is { } oldTarget &&
            TryComp<K9GrabbedComponent>(oldTarget, out var oldGrab))
        {
            ReleaseGrab(oldTarget, oldGrab);
        }

        var grab = EnsureComp<K9GrabbedComponent>(target);
        grab.Dog = ent.Owner;
        grab.GrabTime = _timing.CurTime;
        ent.Comp.GrabbedTarget = target;
        Dirty(target, grab);
        Dirty(ent);

        _movementSpeed.RefreshMovementSpeedModifiers(target);
        _pulling.TryStartPull(ent.Owner, target);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Voice/Vulpkanin/dog_snarl1.ogg"), ent.Owner);

        _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-success-self", ("target", target)), ent.Owner, ent.Owner, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-success-target", ("dog", ent.Owner)), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-success-others", ("dog", ent.Owner), ("target", target)), ent.Owner, FilterOthers(ent.Owner, target), true, PopupType.SmallCaution);
    }

    private void OnGrabbedMoveInput(Entity<K9GrabbedComponent> ent, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement || ent.Comp.Escaping)
            return;

        if (_net.IsClient)
            return;

        ent.Comp.Escaping = true;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-escape-attempt"), ent.Owner, ent.Owner, PopupType.Medium);

        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.EscapeDuration, new K9EscapeGrabDoAfterEvent(), ent.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = false
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnEscapeDoAfter(Entity<K9GrabbedComponent> ent, ref K9EscapeGrabDoAfterEvent args)
    {
        ent.Comp.Escaping = false;
        Dirty(ent);

        if (args.Cancelled || args.Handled)
            return;

        _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-escaped-self"), ent.Owner, ent.Owner, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-escaped-dog", ("target", ent.Owner)), ent.Comp.Dog, ent.Comp.Dog, PopupType.MediumCaution);

        ReleaseGrab(ent.Owner, ent.Comp);
    }

    private void OnGrabbedAttemptStopPulling(Entity<K9GrabbedComponent> ent, ref AttemptStopPullingEvent args)
    {
        // Prevent breaking pull while grabbed unless explicitly released or dog drops it
        if (args.User == ent.Owner || args.User == null)
        {
            args.Cancelled = true;
        }
    }

    private void OnGrabbedPullStopped(Entity<K9GrabbedComponent> ent, ref PullStoppedMessage args)
    {
        // If the pull was somehow interrupted (e.g. distance or mechanics) and target is still grabbed, re-pull
        if (_net.IsServer && Exists(ent.Comp.Dog) && !ent.Comp.Escaping)
        {
            _pulling.TryStartPull(ent.Comp.Dog, ent.Owner);
        }
    }

    private void OnDogDamageChanged(Entity<K9DogComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || ent.Comp.GrabbedTarget is not { } target)
            return;

        if (args.DamageDelta != null && args.DamageDelta.GetTotal() > 10)
        {
            if (TryComp<K9GrabbedComponent>(target, out var grab))
            {
                _popup.PopupEntity(Loc.GetString("rmc-k9-arm-grab-broken-damage", ("dog", ent.Owner)), ent.Owner, PopupType.MediumCaution);
                ReleaseGrab(target, grab);
            }
        }
    }

    private void OnDogKnockedDown(Entity<K9DogComponent> ent, ref KnockedDownEvent args)
    {
        if (ent.Comp.GrabbedTarget is { } target && TryComp<K9GrabbedComponent>(target, out var grab))
        {
            ReleaseGrab(target, grab);
        }
    }

    private void OnDogStunned(Entity<K9DogComponent> ent, ref StunnedEvent args)
    {
        if (ent.Comp.GrabbedTarget is { } target && TryComp<K9GrabbedComponent>(target, out var grab))
        {
            ReleaseGrab(target, grab);
        }
    }

    public void ReleaseGrab(EntityUid target, K9GrabbedComponent? grab = null)
    {
        if (!Resolve(target, ref grab, false))
            return;

        if (TryComp<K9DogComponent>(grab.Dog, out var dogComp) && dogComp.GrabbedTarget == target)
        {
            dogComp.GrabbedTarget = null;
            Dirty(grab.Dog, dogComp);
        }

        if (TryComp<PullableComponent>(target, out var pullable))
        {
            _pulling.TryStopPull(target, pullable);
        }

        RemCompDeferred<K9GrabbedComponent>(target);
        _movementSpeed.RefreshMovementSpeedModifiers(target);
    }

    #endregion

    #region Track & Request Master

    private void OnTrackMasterAction(Entity<K9DogComponent> ent, ref K9TrackMasterActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        if (ent.Comp.Master is not { } master || !Exists(master))
        {
            _popup.PopupEntity(Loc.GetString("rmc-k9-track-master-none"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        var dogCoords = _transform.GetMapCoordinates(ent.Owner);
        var masterCoords = _transform.GetMapCoordinates(master);

        if (dogCoords.MapId != masterCoords.MapId)
        {
            _popup.PopupEntity(Loc.GetString("rmc-k9-track-master-lost"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        var offset = masterCoords.Position - dogCoords.Position;
        var dist = offset.Length();
        var dirStr = GetDirectionString(offset);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Voice/Vulpkanin/dog_bark2.ogg"), ent.Owner);
        _popup.PopupEntity(Loc.GetString("rmc-k9-track-master-result", ("direction", dirStr), ("distance", (int) Math.Round(dist))), ent.Owner, ent.Owner, PopupType.Medium);
    }

    private void OnRequestMasterAction(Entity<K9DogComponent> ent, ref K9RequestMasterActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!target.IsValid() || target == ent.Owner)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        if (ent.Comp.Master != null && Exists(ent.Comp.Master.Value))
        {
            _popup.PopupEntity(Loc.GetString("rmc-k9-request-master-already-bound"), ent.Owner, ent.Owner);
            return;
        }

        if (HasComp<XenoComponent>(target) || !HasComp<MarineComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("rmc-k9-request-master-not-marine"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        var dogNet = GetNetEntity(ent.Owner);
        var marineNet = GetNetEntity(target);

        // Open on the marine so they confirm; event is raised on that same entity.
        _dialog.OpenConfirmation(target, target,
            Loc.GetString("rmc-k9-request-master-title"),
            Loc.GetString("rmc-k9-request-master-prompt", ("dog", ent.Owner)),
            new K9MasterRequestConfirmEvent(dogNet, marineNet));

        _popup.PopupEntity(Loc.GetString("rmc-k9-request-master-sent", ("target", target)), ent.Owner, ent.Owner, PopupType.Medium);
    }

    private void OnMasterRequestConfirmed(Entity<MarineComponent> marine, ref K9MasterRequestConfirmEvent args)
    {
        var dog = GetEntity(args.DogNet);
        var master = GetEntity(args.MarineNet);

        if (!Exists(dog) || !Exists(master) || master != marine.Owner)
            return;

        if (!TryComp<K9DogComponent>(dog, out var dogComp))
            return;

        BindMaster(dog, dogComp, master, asHandler: false);
    }

    private void OnTameAction(Entity<K9HandlerComponent> ent, ref K9TameActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!target.IsValid() || !TryComp<K9DogComponent>(target, out var dogComp))
        {
            _popup.PopupEntity(Loc.GetString("rmc-k9-tame-not-dog"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        if (dogComp.Master != null && Exists(dogComp.Master.Value) && dogComp.HandlerBonded)
        {
            _popup.PopupEntity(Loc.GetString("rmc-k9-tame-already-bound"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        if (_net.IsClient)
            return;

        var handlerNet = GetNetEntity(ent.Owner);
        var dogNet = GetNetEntity(target);

        _dialog.OpenConfirmation(target, target,
            Loc.GetString("rmc-k9-tame-dialog-title"),
            Loc.GetString("rmc-k9-tame-dialog-prompt", ("handler", ent.Owner)),
            new K9TameConfirmEvent(handlerNet, dogNet));

        _popup.PopupEntity(Loc.GetString("rmc-k9-tame-request-sent", ("dog", target)), ent.Owner, ent.Owner, PopupType.Medium);
    }

    private void OnTameConfirmed(Entity<K9DogComponent> ent, ref K9TameConfirmEvent args)
    {
        var handler = GetEntity(args.HandlerNet);
        if (!Exists(handler))
            return;

        BindMaster(ent.Owner, ent.Comp, handler, asHandler: true);
    }

    public void BindMaster(EntityUid dog, K9DogComponent dogComp, EntityUid master, bool asHandler)
    {
        if (dogComp.Master is { } previous && Exists(previous) && previous != master)
        {
            if (_net.IsServer)
            {
                _popup.PopupEntity(Loc.GetString("rmc-k9-bind-replaced-old", ("dog", dog), ("master", master)), previous, previous, PopupType.MediumCaution);
            }
        }

        UnbindMaster(dog, dogComp);

        dogComp.Master = master;
        dogComp.HandlerBonded = asHandler;
        Dirty(dog, dogComp);

        if (asHandler)
        {
            var handler = EnsureComp<K9HandlerComponent>(master);
            handler.Dogs.Add(dog);
            Dirty(master, handler);
        }

        SyncMasterAccess(dog, master);

        if (_net.IsServer)
        {
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Voice/Vulpkanin/dog_bark2.ogg"), dog);
            var masterKey = asHandler ? "rmc-k9-bind-success-handler" : "rmc-k9-bind-success-marine";
            var dogKey = asHandler ? "rmc-k9-bind-success-dog-handler" : "rmc-k9-bind-success-dog-marine";
            _popup.PopupEntity(Loc.GetString(masterKey, ("dog", dog)), master, master, PopupType.Medium);
            _popup.PopupEntity(Loc.GetString(dogKey, ("master", master)), dog, dog, PopupType.Medium);
        }
    }

    private void UnbindMaster(EntityUid dog, K9DogComponent dogComp)
    {
        if (dogComp.Master is { } oldMaster && Exists(oldMaster) &&
            TryComp<K9HandlerComponent>(oldMaster, out var handler))
        {
            if (handler.Dogs.Remove(dog))
                Dirty(oldMaster, handler);
        }

        dogComp.Master = null;
        dogComp.HandlerBonded = false;
        Dirty(dog, dogComp);
    }

    private void SyncMasterAccess(EntityUid dog, EntityUid master)
    {
        var dogAccess = EnsureComp<AccessComponent>(dog);
        var masterTags = _accessReader.FindAccessTags(master);

        dogAccess.Tags.Clear();
        foreach (var tag in masterTags)
        {
            dogAccess.Tags.Add(tag);
        }

        Dirty(dog, dogAccess);
    }

    #endregion

    #region Bodyguard Protocol

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
            return;

        TriggerProtectionRageForMaster(args.Target);
    }

    private void OnHandlerKnockedDown(Entity<K9HandlerComponent> ent, ref KnockedDownEvent args)
    {
        TriggerProtectionRageForMaster(ent.Owner);
    }

    private void OnHandlerStunned(Entity<K9HandlerComponent> ent, ref StunnedEvent args)
    {
        TriggerProtectionRageForMaster(ent.Owner);
    }

    private void OnHandlerPullStarted(Entity<K9HandlerComponent> ent, ref PullStartedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        if (HasComp<K9DogComponent>(args.PullerUid))
            return;

        TriggerProtectionRageForMaster(ent.Owner);
    }

    private void TriggerProtectionRageForMaster(EntityUid master)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<K9HandlerComponent>(master, out var handlerComp))
            return;

        var masterPos = _transform.GetMapCoordinates(master);

        foreach (var dog in handlerComp.Dogs)
        {
            if (!Exists(dog) || !TryComp<K9DogComponent>(dog, out var dogComp))
                continue;

            var dogPos = _transform.GetMapCoordinates(dog);
            if (dogPos.MapId != masterPos.MapId ||
                Vector2.Distance(dogPos.Position, masterPos.Position) > 10f)
                continue;

            var rage = EnsureComp<K9ProtectorRageComponent>(dog);
            rage.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(6);
            Dirty(dog, rage);

            _movementSpeed.RefreshMovementSpeedModifiers(dog);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Voice/Vulpkanin/dog_snarl3.ogg"), dog);
            _popup.PopupEntity(Loc.GetString("rmc-k9-protector-rage-trigger", ("dog", dog)), dog, PopupType.LargeCaution);
        }
    }

    #endregion

    #region Handler Actions

    private void OnSicEmAction(Entity<K9HandlerComponent> ent, ref K9SicEmActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!target.IsValid() || target == ent.Owner)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var marked = EnsureComp<K9MarkedTargetComponent>(target);
        marked.Handler = ent.Owner;
        marked.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(15);
        Dirty(target, marked);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Voice/Vulpkanin/dog_snarl2.ogg"), ent.Owner);

        _popup.PopupEntity(Loc.GetString("rmc-k9-sic-em-shout", ("handler", ent.Owner), ("target", target)), ent.Owner, PopupType.MediumCaution);

        foreach (var dog in ent.Comp.Dogs)
        {
            if (!Exists(dog))
                continue;

            _popup.PopupEntity(Loc.GetString("rmc-k9-sic-em-dog-order", ("target", target)), dog, dog, PopupType.LargeCaution);
        }
    }

    private void OnEvacuateAction(Entity<K9HandlerComponent> ent, ref K9EvacuateActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!target.IsValid())
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var evac = EnsureComp<K9EvacuateTargetComponent>(target);
        evac.Handler = ent.Owner;
        evac.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(30);
        Dirty(target, evac);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Voice/Vulpkanin/dog_bark2.ogg"), ent.Owner);
        _popup.PopupEntity(Loc.GetString("rmc-k9-evacuate-shout", ("handler", ent.Owner), ("target", target)), ent.Owner, PopupType.Medium);

        foreach (var dog in ent.Comp.Dogs)
        {
            if (!Exists(dog))
                continue;

            _popup.PopupEntity(Loc.GetString("rmc-k9-evacuate-dog-order", ("target", target)), dog, dog, PopupType.Medium);
        }
    }

    private void OnGoodBoyAction(Entity<K9HandlerComponent> ent, ref K9GoodBoyActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;
        if (!target.IsValid() || !HasComp<K9DogComponent>(target))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        TriggerGoodBoy(target, ent.Owner);
    }

    private void OnDogInteractUsing(Entity<K9DogComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // If user is feeding a battery / power cell to the synth dog
        if (HasComp<PowerCellComponent>(args.Used))
        {
            args.Handled = true;

            if (_net.IsClient)
                return;

            TriggerGoodBoy(ent.Owner, args.User);
        }
    }

    private void OnDogMeleeHit(Entity<K9DogComponent> ent, ref MeleeHitEvent args)
    {
        if (_net.IsClient || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!hit.IsValid())
                continue;

            SpawnAttachedTo("RMCEffectHeadbite", hit.ToCoordinates());
        }
    }

    public void TriggerGoodBoy(EntityUid dog, EntityUid user)
    {
        if (_net.IsClient)
            return;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/twobeep.ogg"), dog);
        _popup.PopupEntity(Loc.GetString("rmc-k9-good-boy-popup", ("dog", dog), ("user", user)), dog, PopupType.Medium);
    }

    private void OnMarkedTargetDamageModify(Entity<K9MarkedTargetComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is { } attacker &&
            TryComp<K9DogComponent>(attacker, out var dog) &&
            dog.HandlerBonded &&
            dog.Master == ent.Comp.Handler)
        {
            args.Damage *= ent.Comp.DamageBonus;
        }
    }

    #endregion

    #region Update & Acoustic Senses

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // Clean up expired rage
        var rageQuery = EntityQueryEnumerator<K9ProtectorRageComponent>();
        while (rageQuery.MoveNext(out var uid, out var rage))
        {
            if (curTime >= rage.ExpiresAt)
            {
                RemCompDeferred<K9ProtectorRageComponent>(uid);
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }
        }

        // Clean up expired marked targets
        var markedQuery = EntityQueryEnumerator<K9MarkedTargetComponent>();
        while (markedQuery.MoveNext(out var uid, out var marked))
        {
            if (curTime >= marked.ExpiresAt)
            {
                RemCompDeferred<K9MarkedTargetComponent>(uid);
            }
        }

        // Clean up expired evacuate targets
        var evacQuery = EntityQueryEnumerator<K9EvacuateTargetComponent>();
        while (evacQuery.MoveNext(out var uid, out var evac))
        {
            if (curTime >= evac.ExpiresAt)
            {
                RemCompDeferred<K9EvacuateTargetComponent>(uid);
            }
        }

        // Acoustic sensor scanning (Server only)
        if (_net.IsClient)
            return;

        var dogQuery = EntityQueryEnumerator<K9DogComponent, TransformComponent>();
        while (dogQuery.MoveNext(out var uid, out var dog, out var xform))
        {
            if (curTime < dog.NextSensesScan)
                continue;

            dog.NextSensesScan = curTime + dog.SensesScanInterval;

            if (curTime < dog.NextSensesAlert)
                continue;

            // Check if any hostile xeno is within sense radius
            var dogPos = _transform.GetMapCoordinates(xform);
            var foundHostile = false;

            var xenoQuery = EntityQueryEnumerator<XenoComponent, TransformComponent>();
            while (xenoQuery.MoveNext(out var xenoUid, out _, out var xenoXform))
            {
                var xenoPos = _transform.GetMapCoordinates(xenoXform);
                if (xenoPos.MapId != dogPos.MapId)
                    continue;

                if (Vector2.Distance(xenoPos.Position, dogPos.Position) <= dog.SensesRange)
                {
                    foundHostile = true;
                    break;
                }
            }

            if (foundHostile)
            {
                dog.NextSensesAlert = curTime + dog.SensesAlertCooldown;
                Dirty(uid, dog);

                _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Voice/Vulpkanin/dog_growl2.ogg"), uid);
                _popup.PopupEntity(Loc.GetString("rmc-k9-senses-alert-growl", ("dog", uid)), uid, PopupType.MediumCaution);

                if (dog.Master is { } master && Exists(master))
                {
                    _popup.PopupEntity(Loc.GetString("rmc-k9-senses-alert-master", ("dog", uid)), master, master, PopupType.LargeCaution);
                }
            }
        }
    }

    #endregion

    #region Helpers

    private string GetDirectionString(Vector2 offset)
    {
        var dir = offset.GetDir();
        return dir switch
        {
            Direction.East => Loc.GetString("rmc-k9-direction-east"),
            Direction.NorthEast => Loc.GetString("rmc-k9-direction-northeast"),
            Direction.North => Loc.GetString("rmc-k9-direction-north"),
            Direction.NorthWest => Loc.GetString("rmc-k9-direction-northwest"),
            Direction.West => Loc.GetString("rmc-k9-direction-west"),
            Direction.SouthWest => Loc.GetString("rmc-k9-direction-southwest"),
            Direction.South => Loc.GetString("rmc-k9-direction-south"),
            Direction.SouthEast => Loc.GetString("rmc-k9-direction-southeast"),
            _ => Loc.GetString("rmc-k9-direction-north")
        };
    }

    private Filter FilterOthers(EntityUid a, EntityUid b)
    {
        return Filter.PvsExcept(a).RemovePlayerByAttachedEntity(b);
    }

    #endregion
}
