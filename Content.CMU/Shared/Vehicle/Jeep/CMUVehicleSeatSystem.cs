using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
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
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleSystem _vehicles = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> FirearmsSkill = "RMCSkillFirearms";

    private readonly List<(EntityUid Rider, EntityUid Seat)> _exiting = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSeatsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUVehicleSeatsComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<CMUVehicleSeatsComponent, InteractHandEvent>(OnInteractHand, before: [typeof(HardpointSlotSystem)]);
        SubscribeLocalEvent<CMUVehicleSeatsComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUVehicleSeatsComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CMUVehicleSeatComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<CMUVehicleSeatComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<CMUVehicleRiderComponent, DidEquipHandEvent>(OnRiderEquip);
        SubscribeLocalEvent<CMUVehicleRiderComponent, DidUnequipHandEvent>(OnRiderUnequip);
        SubscribeLocalEvent<CMUVehicleRiderGunComponent, GunRefreshModifiersEvent>(OnRiderGunRefresh);
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
            comp.Exit = data.Exit;
            comp.Lift = data.Lift;
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
        if (!ent.Comp.Driver && TryComp(ent.Comp.Vehicle, out CMUVehicleSeatsComponent? seats))
        {
            var rider = EnsureComp<CMUVehicleRiderComponent>(args.Buckle.Owner);
            var skill = _skills.GetSkill(args.Buckle.Owner, FirearmsSkill);
            rider.ExtraSpread = MathF.Max(seats.MinPassengerSpread, seats.PassengerSpread - skill * seats.SpreadPerFirearms);
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
        MarkGun(args.Equipped, ent.Comp.ExtraSpread);
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

    public override void Update(float frameTime)
    {
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
