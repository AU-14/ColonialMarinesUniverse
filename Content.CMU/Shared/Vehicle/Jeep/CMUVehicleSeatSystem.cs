using System.Numerics;
using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using VehicleSystem = Content.Shared.Vehicle.Systems.VehicleSystem;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed class CMUVehicleSeatSystem : EntitySystem
{
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private CMUJeepSystem _jeep = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleSystem _vehicles = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> FirearmsSkill = "RMCSkillFirearms";

    private readonly List<(EntityUid Rider, EntityUid Seat)> _exiting = new();

    // A rider let through a seat's door for this buckle: after the climb, or moving between seats.
    private (EntityUid Rider, EntityUid Vehicle)? _doorPass;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSeatsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUVehicleSeatsComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<CMUVehicleSeatsComponent, InteractHandEvent>(OnInteractHand, before: [typeof(HardpointSlotSystem)]);
        SubscribeLocalEvent<CMUVehicleSeatsComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUVehicleSeatsComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CMUVehicleSeatComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<CMUVehicleSeatComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<CMUVehicleSeatComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<CMUVehicleSeatComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<CMUVehicleSeatComponent, CMUVehicleSeatEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<CMUVehicleSeatComponent, CMUVehicleSeatExitDoAfterEvent>(OnExitDoAfter);
        SubscribeLocalEvent<CMUVehicleSeatedComponent, BeingPulledAttemptEvent>(OnSeatedPullAttempt);
        SubscribeLocalEvent<CMUVehicleRiderComponent, DidEquipHandEvent>(OnRiderEquip);
        SubscribeLocalEvent<CMUVehicleRiderComponent, DidUnequipHandEvent>(OnRiderUnequip);
        SubscribeLocalEvent<CMUVehicleRiderGunComponent, GunRefreshModifiersEvent>(OnRiderGunRefresh);
    }

    /// <summary>
    /// A click on a seat, like on a chair: the clicker sits in an empty seat, gets out of their own,
    /// or unbuckles whoever sits there so they can be dragged out.
    /// </summary>
    public void ClickSeat(EntityUid seat, EntityUid user)
    {
        if (!TryComp(seat, out StrapComponent? strap))
            return;

        if (TryComp(user, out BuckleComponent? buckle) && buckle.BuckledTo == seat)
        {
            _buckle.TryUnbuckle(user, user);
        }
        else if (strap.BuckledEntities.Count == 0)
        {
            // Moving to a free seat inside the jeep needs no door.
            var vehicle = CompOrNull<CMUVehicleSeatComponent>(seat)?.Vehicle;
            if (vehicle != null && IsSeatedIn(user, vehicle.Value))
                _doorPass = (user, vehicle.Value);

            try
            {
                _buckle.TryBuckle(user, user, seat);
            }
            finally
            {
                _doorPass = null;
            }
        }
        else
        {
            _buckle.TryUnbuckle(strap.BuckledEntities.First(), user);
        }
    }

    /// <summary>
    /// Whether a rider has to climb over this seat's shut door to get in or out.
    /// </summary>
    private bool DoorBlocks(CMUVehicleSeatComponent seat, EntityUid rider)
    {
        return seat.Door != null &&
               seat.Vehicle is { } vehicle &&
               _doorPass != (rider, vehicle) &&
               !_jeep.IsPanelOpen(vehicle, seat.Door);
    }

    /// <summary>
    /// Climbing in over a shut door takes a while; the buckle waits for it. Checks that only look
    /// (popup off) and scripted moves (no user) pass straight through.
    /// </summary>
    private void OnStrapAttempt(Entity<CMUVehicleSeatComponent> ent, ref StrapAttemptEvent args)
    {
        if (args.Cancelled || args.User is not { } user || !args.Popup || !DoorBlocks(ent.Comp, args.Buckle))
            return;

        args.Cancelled = true;
        StartClimb(ent, user, args.Buckle, new CMUVehicleSeatEnterDoAfterEvent());
    }

    private void OnUnstrapAttempt(Entity<CMUVehicleSeatComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.Cancelled || args.User is not { } user || !args.Popup || !DoorBlocks(ent.Comp, args.Buckle))
            return;

        args.Cancelled = true;
        StartClimb(ent, user, args.Buckle, new CMUVehicleSeatExitDoAfterEvent());
    }

    private void StartClimb(Entity<CMUVehicleSeatComponent> seat, EntityUid user, EntityUid rider, DoAfterEvent ev)
    {
        if (!TryComp(seat.Comp.Vehicle, out CMUVehicleSeatsComponent? seats))
            return;

        // One click can try the same unbuckle twice; the second must not cancel the first.
        var args = new DoAfterArgs(EntityManager, user, seats.ClosedDoorDelay, ev, seat, rider, seat)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = false,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnEnterDoAfter(Entity<CMUVehicleSeatComponent> ent, ref CMUVehicleSeatEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } rider || ent.Comp.Vehicle is not { } vehicle)
            return;

        args.Handled = true;
        if (TryComp(rider, out BuckleComponent? buckle) && buckle.BuckledTo == ent.Owner)
            return;

        _doorPass = (rider, vehicle);
        try
        {
            _buckle.TryBuckle(rider, args.User, ent);
        }
        finally
        {
            _doorPass = null;
        }
    }

    private void OnExitDoAfter(Entity<CMUVehicleSeatComponent> ent, ref CMUVehicleSeatExitDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } rider || ent.Comp.Vehicle is not { } vehicle)
            return;

        args.Handled = true;
        if (!TryComp(rider, out BuckleComponent? buckle) || buckle.BuckledTo != ent.Owner)
            return;

        _doorPass = (rider, vehicle);
        try
        {
            _buckle.TryUnbuckle(rider, args.User);
        }
        finally
        {
            _doorPass = null;
        }
    }

    /// <summary>
    /// Nobody drags a rider out over a shut door; it has to be opened, or the rider unbuckled first.
    /// </summary>
    private void OnSeatedPullAttempt(Entity<CMUVehicleSeatedComponent> ent, ref BeingPulledAttemptEvent args)
    {
        if (TryComp(ent, out BuckleComponent? buckle) &&
            TryComp(buckle.BuckledTo, out CMUVehicleSeatComponent? seat) &&
            DoorBlocks(seat, ent))
        {
            args.Cancel();
        }
    }


    private void OnMapInit(Entity<CMUVehicleSeatsComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.SeatEntities.Count > 0)
            return;

        foreach (var data in ent.Comp.Seats)
        {
            var seat = SpawnAttachedTo(data.Prototype ?? ent.Comp.SeatPrototype, new EntityCoordinates(ent, data.Offset));
            _metaData.SetEntityName(seat, Loc.GetString(data.Name));

            var comp = EnsureComp<CMUVehicleSeatComponent>(seat);
            comp.Vehicle = ent;
            comp.Driver = data.Driver;
            comp.Door = data.Door;
            comp.Exit = data.Exit;
            comp.Lift = data.Lift;
            comp.PixelOffsets = new Dictionary<Direction, Vector2>(data.PixelOffsets);
            comp.Clips = new Dictionary<Direction, float>(data.Clips);
            Dirty(seat, comp);

            var part = EnsureComp<CMUVehiclePartComponent>(seat);
            part.Vehicle = ent;
            part.Part = $"seat_{data.Id}";
            Dirty(seat, part);

            ent.Comp.SeatEntities.Add(seat);
        }

        Dirty(ent);
    }

    private void OnTerminating(Entity<CMUVehicleSeatsComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_net.IsClient)
            return;

        foreach (var seat in ent.Comp.SeatEntities)
        {
            if (!TryComp(seat, out StrapComponent? strap))
                continue;

            foreach (var rider in strap.BuckledEntities.ToArray())
            {
                _buckle.TryUnbuckle(rider, null, popup: false);
                _transform.AttachToGridOrMap(rider);
            }
        }
    }

    private void OnInteractHand(Entity<CMUVehicleSeatsComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        args.Handled = true;

        if (IsSeatedIn(user, ent))
        {
            _buckle.TryUnbuckle(user, user);
            return;
        }

        if (TryGetFreeSeat(ent, user, out var seat))
        {
            _buckle.TryBuckle(user, user, seat);
            return;
        }

        _popup.PopupClient(Loc.GetString("cmu-vehicle-seat-none-free"), ent, user);
    }

    private void OnGetVerbs(Entity<CMUVehicleSeatsComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (IsSeatedIn(user, ent))
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-vehicle-seat-exit-verb"),
                Act = () => _buckle.TryUnbuckle(user, user),
            });
            return;
        }

        foreach (var seat in ent.Comp.SeatEntities)
        {
            if (!IsFree(seat))
                continue;

            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-vehicle-seat-verb", ("seat", Name(seat))),
                Act = () => _buckle.TryBuckle(user, user, seat),
            });
        }
    }

    private void OnPreventCollide(Entity<CMUVehicleSeatsComponent> ent, ref PreventCollideEvent args)
    {
        if (IsAboard(args.OtherEntity, ent))
            args.Cancelled = true;
    }

    private void OnStrapped(Entity<CMUVehicleSeatComponent> ent, ref StrappedEvent args)
    {
        EnsureComp<CMUVehicleSeatedComponent>(args.Buckle.Owner);
        if (TryComp(ent.Comp.Vehicle, out CMUVehicleSeatsComponent? seats))
        {
            var rider = EnsureComp<CMUVehicleRiderComponent>(args.Buckle.Owner);
            var skill = _skills.GetSkill(args.Buckle.Owner, FirearmsSkill);
            rider.ExtraSpread = MathF.Max(seats.MinPassengerSpread, seats.PassengerSpread - skill * seats.SpreadPerFirearms);
            rider.SpeedStep = 0;
            Dirty(args.Buckle.Owner, rider);
            foreach (var held in _hands.EnumerateHeld(args.Buckle.Owner))
            {
                MarkGun(held, rider.ExtraSpread);
            }
        }

        if (_net.IsClient ||
            !ent.Comp.Driver ||
            ent.Comp.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out VehicleComponent? vehicleComp))
        {
            return;
        }

        _vehicles.TrySetOperator((vehicle, vehicleComp), args.Buckle.Owner);
    }

    private void OnUnstrapped(Entity<CMUVehicleSeatComponent> ent, ref UnstrappedEvent args)
    {
        RemComp<CMUVehicleSeatedComponent>(args.Buckle.Owner);
        if (RemComp<CMUVehicleRiderComponent>(args.Buckle.Owner))
        {
            foreach (var held in _hands.EnumerateHeld(args.Buckle.Owner))
            {
                UnmarkGun(held);
            }
        }

        if (_net.IsClient || ent.Comp.Vehicle is not { } vehicle)
            return;

        var rider = args.Buckle.Owner;
        if (ent.Comp.Driver &&
            TryComp(vehicle, out VehicleComponent? vehicleComp) &&
            vehicleComp.Operator == rider)
        {
            _vehicles.TryRemoveOperator((vehicle, vehicleComp));
        }

        if (!Terminating(vehicle))
            _exiting.Add((rider, ent.Owner));
    }

    private void OnRiderEquip(Entity<CMUVehicleRiderComponent> ent, ref DidEquipHandEvent args)
    {
        MarkGun(args.Equipped, RiderSpread(ent));
    }

    /// <summary>
    /// A rider's spread at the vehicle's current speed.
    /// </summary>
    private float RiderSpread(Entity<CMUVehicleRiderComponent> rider)
    {
        var moving = TryComp(rider, out BuckleComponent? buckle) &&
                     TryComp(buckle.BuckledTo, out CMUVehicleSeatComponent? seat) &&
                     TryComp(seat.Vehicle, out CMUVehicleSeatsComponent? seats)
            ? seats.MovingSpread
            : 0f;
        return rider.Comp.ExtraSpread + moving * rider.Comp.SpeedStep / SpeedSteps;
    }

    private void OnRiderUnequip(Entity<CMUVehicleRiderComponent> ent, ref DidUnequipHandEvent args)
    {
        UnmarkGun(args.Unequipped);
    }

    private void OnRiderGunRefresh(Entity<CMUVehicleRiderGunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var extra = Angle.FromDegrees(ent.Comp.ExtraSpread);
        args.MinAngle += extra;
        args.MaxAngle += extra;
        args.CameraRecoilScalar = 0f;
    }

    private void MarkGun(EntityUid item, float spread)
    {
        if (!HasComp<GunComponent>(item))
            return;

        var marker = EnsureComp<CMUVehicleRiderGunComponent>(item);
        marker.ExtraSpread = spread;
        Dirty(item, marker);
        _gun.RefreshModifiers(item);
    }

    private void UnmarkGun(EntityUid item)
    {
        if (RemComp<CMUVehicleRiderGunComponent>(item))
            _gun.RefreshModifiers(item);
    }

    /// <summary>
    /// Drops a rider's pending exit move, for a rider who was moved off their seat on purpose.
    /// </summary>
    public void CancelExit(EntityUid rider)
    {
        _exiting.RemoveAll(e => e.Rider == rider);
    }

    // Speed is followed in fifths of top speed, so guns are refreshed only when it changes that much.
    private const int SpeedSteps = 5;

    public override void Update(float frameTime)
    {
        UpdateRiderSpread();
        if (_exiting.Count == 0)
            return;

        foreach (var (rider, seat) in _exiting)
        {
            if (TerminatingOrDeleted(rider) ||
                !TryComp(seat, out CMUVehicleSeatComponent? comp) ||
                comp.Vehicle is not { } vehicle ||
                TerminatingOrDeleted(vehicle) ||
                TryComp(rider, out BuckleComponent? buckle) && buckle.Buckled)
            {
                continue;
            }

            _transform.SetCoordinates(rider, new EntityCoordinates(vehicle, comp.Exit));
            _transform.AttachToGridOrMap(rider);
        }

        _exiting.Clear();
    }

    /// <summary>
    /// Widens riders' guns as their vehicle speeds up, and narrows them as it slows.
    /// </summary>
    private void UpdateRiderSpread()
    {
        var riders = EntityQueryEnumerator<CMUVehicleRiderComponent, BuckleComponent>();
        while (riders.MoveNext(out var uid, out var rider, out var buckle))
        {
            if (!TryComp(buckle.BuckledTo, out CMUVehicleSeatComponent? seat) ||
                !TryComp(seat.Vehicle, out GridVehicleMoverComponent? mover) ||
                mover.MaxSpeed <= 0f)
            {
                continue;
            }

            var step = (int) MathF.Round(Math.Clamp(MathF.Abs(mover.CurrentSpeed) / mover.MaxSpeed, 0f, 1f) * SpeedSteps);
            if (step == rider.SpeedStep)
                continue;

            rider.SpeedStep = step;
            var spread = RiderSpread((uid, rider));
            foreach (var held in _hands.EnumerateHeld(uid))
            {
                MarkGun(held, spread);
            }
        }
    }

    private bool TryGetFreeSeat(Entity<CMUVehicleSeatsComponent> ent, EntityUid user, out EntityUid seat)
    {
        seat = default;
        var userPos = _transform.GetWorldPosition(user);
        var best = float.MaxValue;
        foreach (var candidate in ent.Comp.SeatEntities)
        {
            if (!IsFree(candidate))
                continue;

            var distance = (_transform.GetWorldPosition(candidate) - userPos).LengthSquared();
            if (distance >= best)
                continue;

            best = distance;
            seat = candidate;
        }

        return seat.IsValid();
    }

    private bool IsFree(EntityUid seat)
    {
        return TryComp(seat, out StrapComponent? strap) && strap.BuckledEntities.Count == 0;
    }

    private bool IsSeatedIn(EntityUid user, EntityUid vehicle)
    {
        return TryComp(user, out BuckleComponent? buckle) &&
               TryComp(buckle.BuckledTo, out CMUVehicleSeatComponent? seat) &&
               seat.Vehicle == vehicle;
    }

    private bool IsAboard(EntityUid other, EntityUid vehicle)
    {
        var parent = Transform(other).ParentUid;
        for (var i = 0; i < 4 && parent.IsValid(); i++)
        {
            if (parent == vehicle)
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }
}
