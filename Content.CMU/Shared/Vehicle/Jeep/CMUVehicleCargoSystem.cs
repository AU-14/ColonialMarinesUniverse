using Content.Shared.Construction.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed class CMUVehicleCargoSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleCargoComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUVehicleCargoComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(AnchorableSystem)]);
        SubscribeLocalEvent<CMUVehicleCargoComponent, CMUVehicleCargoLoadDoAfterEvent>(OnLoad);
        SubscribeLocalEvent<CMUVehicleCargoComponent, CMUVehicleCargoSecureDoAfterEvent>(OnSecure);
        SubscribeLocalEvent<CMUVehicleCargoComponent, EntityTerminatingEvent>(OnVehicleTerminating);
        SubscribeLocalEvent<CMUVehicleCargoCrateComponent, InteractUsingEvent>(OnCrateInteractUsing, before: [typeof(AnchorableSystem)]);
        SubscribeLocalEvent<CMUVehicleCargoCrateComponent, BeingPulledAttemptEvent>(OnCratePullAttempt);
        SubscribeLocalEvent<CMUVehicleCargoCrateComponent, EntityTerminatingEvent>(OnCrateTerminating);
    }

    private void OnGetVerbs(Entity<CMUVehicleCargoComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (ent.Comp.Crate == null &&
            TryComp(user, out PullerComponent? puller) &&
            puller.Pulling is { } pulled &&
            _whitelist.IsWhitelistPass(ent.Comp.Whitelist, pulled))
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
                Act = () => Unload(ent, crate),
            });
        }
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

        if (TryComp(crate, out PullableComponent? pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(crate, pullable);

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

    public void Unload(Entity<CMUVehicleCargoComponent> ent, EntityUid crate)
    {
        if (_net.IsClient || ent.Comp.Crate != crate)
            return;

        ent.Comp.Crate = null;
        ent.Comp.Secured = false;
        Dirty(ent);

        if (TryComp(crate, out CMUVehicleCargoCrateComponent? cargo))
        {
            if (TryComp(crate, out PhysicsComponent? body))
            {
                _physics.SetBodyType(crate, cargo.BodyType, body: body);
                _physics.SetCanCollide(crate, cargo.CanCollide, body: body);
            }

            RemComp<CMUVehicleCargoCrateComponent>(crate);
        }

        if (TerminatingOrDeleted(ent))
        {
            _transform.AttachToGridOrMap(crate);
            return;
        }

        _transform.SetCoordinates(crate, new EntityCoordinates(ent, ent.Comp.DropOffset));
        _transform.AttachToGridOrMap(crate);
    }

    private void OnInteractUsing(Entity<CMUVehicleCargoComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Crate == null || !_tool.HasQuality(args.Used, ent.Comp.SecureQuality))
            return;

        args.Handled = StartSecure(ent, args.User, args.Used);
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

    private void OnCratePullAttempt(Entity<CMUVehicleCargoCrateComponent> ent, ref BeingPulledAttemptEvent args)
    {
        args.Cancel();
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
        if (ent.Comp.Crate is { } crate)
            Unload(ent, crate);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUVehicleCargoComponent, GridVehicleMoverComponent>();
        while (query.MoveNext(out var uid, out var cargo, out var mover))
        {
            if (cargo.Crate is not { } crate || cargo.Secured || MathF.Abs(mover.CurrentSpeed) < cargo.SlideOffSpeed)
                continue;

            Unload((uid, cargo), crate);
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-cargo-slid-off"), uid, PopupType.MediumCaution);
        }
    }
}
