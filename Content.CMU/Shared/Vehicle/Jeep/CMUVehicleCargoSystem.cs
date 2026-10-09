using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed class CMUVehicleCargoSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUJeepSystem _jeep = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private const CollisionGroup DropBlockers =
        CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.LowImpassable;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleCargoComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUVehicleCargoComponent, InteractUsingEvent>(OnInteractUsing,
            before: [typeof(AnchorableSystem), typeof(HardpointSystem)]);
        SubscribeLocalEvent<CMUVehicleCargoComponent, CMUVehicleCargoLoadDoAfterEvent>(OnLoad);
        SubscribeLocalEvent<CMUVehicleCargoComponent, CMUVehicleCargoSecureDoAfterEvent>(OnSecure);
        SubscribeLocalEvent<CMUVehicleCargoComponent, EntityTerminatingEvent>(OnVehicleTerminating);
        // Ordering must match the vehicle subscription above; the event bus rejects mismatched orderings.
        SubscribeLocalEvent<CMUVehicleCargoCrateComponent, InteractUsingEvent>(OnCrateInteractUsing,
            before: [typeof(AnchorableSystem), typeof(HardpointSystem)]);
        SubscribeLocalEvent<CMUVehicleCargoCrateComponent, BeingPulledAttemptEvent>(OnCratePullAttempt);
        SubscribeLocalEvent<CMUVehicleCargoCrateComponent, RMCGetPullTargetEvent>(OnCrateGrabbed);
        SubscribeLocalEvent<CMUVehicleCargoCrateComponent, EntParentChangedMessage>(OnCrateParentChanged);
        SubscribeLocalEvent<CMUVehicleCargoCrateComponent, EntityTerminatingEvent>(OnCrateTerminating);
        // RMCPullingSystem owns the puller's PullStoppedMessage; the marker stays and is checked against the pull.
        SubscribeLocalEvent<PullerComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<CMUVehicleCargoPulledComponent, ShouldHandleVirtualItemInteractEvent>(OnPulledShouldHandle);
        SubscribeLocalEvent<CMUVehicleCargoPulledComponent, BeforeRangedInteractEvent>(OnPulledInteract);
    }

    private void OnGetVerbs(Entity<CMUVehicleCargoComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // The bed is loaded and unloaded over the tailgate.
        if (!IsTailgateOpen(ent))
            return;

        var user = args.User;
        if (ent.Comp.Crate == null &&
            TryComp(user, out PullerComponent? puller) &&
            puller.Pulling is { } pulled &&
            CanLoad(ent, user, pulled))
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-vehicle-cargo-load-verb"),
                Act = () => StartLoad(ent, user, pulled),
            });
        }

        if (ent.Comp.Crate is { } crate && !ent.Comp.Secured)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-vehicle-cargo-unload-verb"),
                Act = () => Unload(ent, crate, user),
            });
        }
    }

    private bool CanLoad(Entity<CMUVehicleCargoComponent> ent, EntityUid user, EntityUid crate)
    {
        return !HasComp<CMUVehicleCargoCrateComponent>(crate) &&
               !Transform(crate).Anchored &&
               !_container.IsEntityInContainer(crate) &&
               TryComp(crate, out PullableComponent? pullable) &&
               pullable.Puller == user &&
               _whitelist.IsWhitelistPass(ent.Comp.Whitelist, crate);
    }

    private void StartLoad(Entity<CMUVehicleCargoComponent> ent, EntityUid user, EntityUid crate)
    {
        var args = new DoAfterArgs(EntityManager, user, ent.Comp.LoadDelay, new CMUVehicleCargoLoadDoAfterEvent(), ent, ent, crate)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(args);
    }

    private void OnLoad(Entity<CMUVehicleCargoComponent> ent, ref CMUVehicleCargoLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } crate || ent.Comp.Crate != null)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        // The crate can change hands, get anchored or boxed up, or the tailgate shut, while the
        // do-after runs.
        if (!CanLoad(ent, args.User, crate) || !IsTailgateOpen(ent))
            return;

        if (TryComp(crate, out PullableComponent? pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(crate, pullable);

        // Set before the move so OnCrateParentChanged sees the vehicle as the expected parent.
        var cargo = EnsureComp<CMUVehicleCargoCrateComponent>(crate);
        cargo.Vehicle = ent;
        if (TryComp(crate, out PhysicsComponent? body))
        {
            cargo.BodyType = body.BodyType;
            cargo.CanCollide = body.CanCollide;
            _physics.SetCanCollide(crate, false, body: body);
            _physics.SetBodyType(crate, Robust.Shared.Physics.BodyType.Static, body: body);
        }

        Dirty(crate, cargo);
        _transform.SetCoordinates(crate, new EntityCoordinates(ent, default));
        _transform.SetLocalRotation(crate, Angle.Zero);

        ent.Comp.Crate = crate;
        ent.Comp.Secured = false;
        Dirty(ent);
    }

    /// <summary>
    /// Sets the crate down behind the vehicle. Returns false and leaves it loaded when that spot is blocked.
    /// </summary>
    public bool Unload(Entity<CMUVehicleCargoComponent> ent, EntityUid crate, EntityUid? user = null)
    {
        if (_net.IsClient || ent.Comp.Crate != crate)
            return false;

        var terminating = TerminatingOrDeleted(ent);
        var drop = new EntityCoordinates(ent, ent.Comp.DropOffset);
        if (!terminating && !CanDrop(ent, crate, drop))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("cmu-vehicle-cargo-unload-blocked"), ent, user.Value, PopupType.SmallCaution);

            return false;
        }

        ent.Comp.Crate = null;
        ent.Comp.Secured = false;
        Dirty(ent);

        // Released before the move so OnCrateParentChanged doesn't treat it as the crate being taken.
        if (TryComp(crate, out CMUVehicleCargoCrateComponent? cargo))
            ReleaseCrate((crate, cargo));

        if (terminating)
        {
            _transform.AttachToGridOrMap(crate);
            return true;
        }

        _transform.SetCoordinates(crate, drop);
        _transform.AttachToGridOrMap(crate);
        return true;
    }

    private bool CanDrop(Entity<CMUVehicleCargoComponent> ent, EntityUid crate, EntityCoordinates drop)
    {
        if (!_interaction.InRangeUnobstructed(ent.Owner,
                _transform.ToMapCoordinates(drop),
                range: ent.Comp.DropOffset.Length() + 0.5f,
                collisionMask: DropBlockers,
                predicate: uid => uid == crate))
        {
            return false;
        }

        return _turf.TryGetTileRef(drop, out var tile) &&
               !tile.Value.Tile.IsEmpty &&
               !_turf.IsTileBlocked(tile.Value.GridUid,
                   tile.Value.GridIndices,
                   DropBlockers,
                   ignore: uid => uid == ent.Owner || uid == crate);
    }

    /// <summary>
    /// Gives the crate back the physics it had before loading and clears its riding state.
    /// </summary>
    private void ReleaseCrate(Entity<CMUVehicleCargoCrateComponent> crate)
    {
        if (TryComp(crate, out PhysicsComponent? body))
        {
            // Anchoring makes the body static before it reparents.
            if (!Transform(crate).Anchored)
                _physics.SetBodyType(crate, crate.Comp.BodyType, body: body);

            // Containers keep their contents non-colliding and wake them again on removal.
            if (!_container.IsEntityInContainer(crate))
                _physics.SetCanCollide(crate, crate.Comp.CanCollide, body: body);
        }

        RemComp<CMUVehicleCargoCrateComponent>(crate);
    }

    private void OnInteractUsing(Entity<CMUVehicleCargoComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Crate == null || !_tool.HasQuality(args.Used, ent.Comp.SecureQuality))
            return;

        // Frame and failure repairs also take a wrench to the body; the crate itself can still be wrenched.
        if (HasPendingRepair(ent, args.Used))
            return;

        args.Handled = StartSecure(ent, args.User, args.Used);
    }

    private bool HasPendingRepair(EntityUid vehicle, EntityUid tool)
    {
        if (TryComp(vehicle, out HardpointIntegrityComponent? integrity) &&
            integrity.MaxIntegrity > 0f &&
            integrity.Integrity < integrity.MaxIntegrity - integrity.FrameRepairEpsilon)
        {
            return true;
        }

        return TryComp(vehicle, out HardpointSlotsComponent? slots) &&
               _hardpoints.HasMatchingFailureRepairStepInTree(vehicle, slots, tool);
    }

    private void OnCrateInteractUsing(Entity<CMUVehicleCargoCrateComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp(ent.Comp.Vehicle, out CMUVehicleCargoComponent? cargo) ||
            !_tool.HasQuality(args.Used, cargo.SecureQuality))
        {
            return;
        }

        args.Handled = StartSecure((ent.Comp.Vehicle.Value, cargo), args.User, args.Used);
    }

    private bool StartSecure(Entity<CMUVehicleCargoComponent> ent, EntityUid user, EntityUid tool)
    {
        return _tool.UseTool(tool, user, ent, (float) ent.Comp.SecureDelay.TotalSeconds, ent.Comp.SecureQuality,
            new CMUVehicleCargoSecureDoAfterEvent());
    }

    private void OnSecure(Entity<CMUVehicleCargoComponent> ent, ref CMUVehicleCargoSecureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Crate == null)
            return;

        args.Handled = true;
        ent.Comp.Secured = !ent.Comp.Secured;
        Dirty(ent);
        var message = ent.Comp.Secured ? "cmu-vehicle-cargo-secured" : "cmu-vehicle-cargo-released";
        _popup.PopupClient(Loc.GetString(message), ent, args.User);
    }

    private void OnPullStarted(Entity<PullerComponent> ent, ref PullStartedMessage args)
    {
        if (args.PullerUid == ent.Owner && HasComp<EntityStorageComponent>(args.PulledUid))
            EnsureComp<CMUVehicleCargoPulledComponent>(args.PulledUid);
    }

    /// <summary>
    /// The pull's virtual item blocks clicks of its own; RMC hands them to the pulled entity when it asks.
    /// </summary>
    private void OnPulledShouldHandle(Entity<CMUVehicleCargoPulledComponent> ent, ref ShouldHandleVirtualItemInteractEvent args)
    {
        if (IsPulledBy(ent, args.Event.User) && TryGetCargoBed(args.Event.Target, out _))
            args.Handle = true;
    }

    /// <summary>
    /// Clicking a jeep, or any of its parts, with the hand that pulls a crate loads the crate. The part
    /// masks have no body and sit at the jeep's centre, so reach is measured to the jeep's own body:
    /// standing behind the bed is close enough.
    /// </summary>
    private void OnPulledInteract(Entity<CMUVehicleCargoPulledComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (!IsPulledBy(ent, args.User) ||
            !TryGetCargoBed(args.Target, out var bed) ||
            !args.CanReach && !_interaction.InRangeUnobstructed(args.User, bed.Owner))
        {
            return;
        }

        args.Handled = true;
        if (bed.Comp.Crate != null)
            _popup.PopupClient(Loc.GetString("cmu-vehicle-cargo-full"), bed, args.User);
        else if (!IsTailgateOpen(bed))
            _popup.PopupClient(Loc.GetString("cmu-vehicle-cargo-tailgate-closed"), bed, args.User);
        else if (CanLoad(bed, args.User, ent))
            StartLoad(bed, args.User, ent);
    }

    /// <summary>
    /// Whether the bed's tailgate is down or off; a vehicle without one is always open.
    /// </summary>
    private bool IsTailgateOpen(EntityUid vehicle)
    {
        return _jeep.IsPanelOpen(vehicle, CMUJeepSystem.Tailgate);
    }

    private bool IsPulledBy(EntityUid crate, EntityUid user)
    {
        return TryComp(crate, out PullableComponent? pullable) && pullable.Puller == user;
    }

    private bool TryGetCargoBed(EntityUid? target, out Entity<CMUVehicleCargoComponent> bed)
    {
        bed = default;
        if (target is not { } clicked)
            return false;

        var vehicle = CompOrNull<CMUVehiclePartComponent>(clicked)?.Vehicle ?? clicked;
        if (!TryComp(vehicle, out CMUVehicleCargoComponent? cargo))
            return false;

        bed = (vehicle, cargo);
        return true;
    }

    /// <summary>
    /// Grabbing a loose crate on the bed sets it down behind the vehicle, so the pull then takes it
    /// away. Raised only when someone actually starts a pull, before the pull is checked; a crate
    /// wrenched down stays on the bed.
    /// </summary>
    private void OnCrateGrabbed(Entity<CMUVehicleCargoCrateComponent> ent, ref RMCGetPullTargetEvent args)
    {
        if (args.Target != ent.Owner ||
            !TryComp(ent.Comp.Vehicle, out CMUVehicleCargoComponent? cargo) ||
            cargo.Crate != ent.Owner)
        {
            return;
        }

        if (cargo.Secured)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-cargo-wrenched"), ent, args.User);
            return;
        }

        if (!IsTailgateOpen(ent.Comp.Vehicle.Value))
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-cargo-tailgate-closed"), ent, args.User);
            return;
        }

        Unload((ent.Comp.Vehicle.Value, cargo), ent, args.User);
    }

    private void OnCratePullAttempt(Entity<CMUVehicleCargoCrateComponent> ent, ref BeingPulledAttemptEvent args)
    {
        args.Cancel();
    }

    /// <summary>
    /// Anything other than Unload taking the crate off the bed (power loader, fulton, anchoring) frees the bed
    /// and gives the crate its physics back.
    /// </summary>
    private void OnCrateParentChanged(Entity<CMUVehicleCargoCrateComponent> ent, ref EntParentChangedMessage args)
    {
        // Server only: the client applies parent and component states in any order.
        if (_net.IsClient || args.Transform.ParentUid == ent.Comp.Vehicle || TerminatingOrDeleted(ent))
            return;

        if (TryComp(ent.Comp.Vehicle, out CMUVehicleCargoComponent? cargo) && cargo.Crate == ent.Owner)
        {
            cargo.Crate = null;
            cargo.Secured = false;
            Dirty(ent.Comp.Vehicle.Value, cargo);
        }

        ReleaseCrate(ent);
    }

    private void OnCrateTerminating(Entity<CMUVehicleCargoCrateComponent> ent, ref EntityTerminatingEvent args)
    {
        if (TryComp(ent.Comp.Vehicle, out CMUVehicleCargoComponent? cargo) && cargo.Crate == ent.Owner)
        {
            cargo.Crate = null;
            cargo.Secured = false;
            Dirty(ent.Comp.Vehicle.Value, cargo);
        }
    }

    private void OnVehicleTerminating(Entity<CMUVehicleCargoComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Crate is not { } crate)
            return;

        // A grid or map being deleted takes the crate down with the jeep; unloading would move it onto a
        // dying parent while that parent's children are being walked.
        var xform = Transform(ent);
        if (TerminatingOrDeleted(xform.MapUid) || (xform.GridUid is { } grid && TerminatingOrDeleted(grid)))
        {
            ent.Comp.Crate = null;
            ent.Comp.Secured = false;
            return;
        }

        Unload(ent, crate);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUVehicleCargoComponent, GridVehicleMoverComponent>();
        while (query.MoveNext(out var uid, out var cargo, out var mover))
        {
            // A loose crate only slides off over a lowered tailgate.
            if (cargo.Crate is not { } crate ||
                cargo.Secured ||
                MathF.Abs(mover.CurrentSpeed) < cargo.SlideOffSpeed ||
                !IsTailgateOpen(uid))
            {
                continue;
            }

            // A blocked drop spot keeps the crate on the bed.
            if (!Unload((uid, cargo), crate))
                continue;

            _popup.PopupEntity(Loc.GetString("cmu-vehicle-cargo-slid-off"), uid, PopupType.MediumCaution);
        }
    }
}
