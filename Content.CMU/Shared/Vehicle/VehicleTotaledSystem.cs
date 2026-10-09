using Content.Shared._RMC14.Explosion;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Vehicle;

public sealed class VehicleTotaledSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private VehicleSystem _rmcVehicles = default!;
    [Dependency] private VehicleTopologySystem _topology = default!;
    [Dependency] private Content.Shared.Vehicle.Systems.VehicleSystem _vehicles = default!;

    // same bar as the tank cook-off, tanks keep their own handling through TankCookOffComponent
    private static readonly ProtoId<ExplosionPrototype>[] CatastrophicExplosions = { "RMCOB", "RMCOBXenoTunnel" };
    private static readonly FixedPoint2 CatastrophicExplosionThreshold = 300;

    // gentler than the cook-off blast, they're just getting out of a dead hull
    private const float EjectionDistance = 1.5f;
    private const float EjectionSpeed = 4f;

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleComponent, ExplosionReceivedEvent>(OnExplosionReceived);
        SubscribeLocalEvent<VehicleTotaledComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<VehicleTotaledComponent, VehicleEntryAttemptEvent>(OnEntryAttempt);
        SubscribeLocalEvent<HardpointItemComponent, ShotAttemptedEvent>(OnHardpointShotAttempted);
    }

    private void OnExplosionReceived(Entity<VehicleComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (_net.IsClient ||
            HasComp<VehicleTotaledComponent>(ent) ||
            HasComp<TankCookOffComponent>(ent) ||
            !HasComp<HardpointSlotsComponent>(ent) ||
            !HasComp<HardpointIntegrityComponent>(ent))
            return;

        if (Array.IndexOf(CatastrophicExplosions, args.Explosion) < 0 ||
            args.Damage.GetTotal() < CatastrophicExplosionThreshold)
            return;

        // deliberately doesn't touch integrity or appearance, it just stops working
        AddComp<VehicleTotaledComponent>(ent);
        _vehicles.RefreshCanRun((ent.Owner, ent.Comp));
        _rmcVehicles.EjectOccupants(ent, EjectionDistance, EjectionSpeed);
    }

    private void OnCanRun(Entity<VehicleTotaledComponent> ent, ref VehicleCanRunEvent args)
    {
        args.CanRun = false;
    }

    // port guns and door guns live on the interior map, keeping people out is what stops those
    private void OnEntryAttempt(Entity<VehicleTotaledComponent> ent, ref VehicleEntryAttemptEvent args)
    {
        args.Cancelled = true;
        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-totaled-entry-blocked"), ent.Owner, args.User, PopupType.SmallCaution);
    }

    // gated on the gun itself so runaway triggers and deployed hardpoints can't fire either, not just seated gunners
    private void OnHardpointShotAttempted(Entity<HardpointItemComponent> ent, ref ShotAttemptedEvent args)
    {
        if (_topology.TryGetVehicle(ent, out var vehicle, includeSelf: false) &&
            HasComp<VehicleTotaledComponent>(vehicle))
            args.Cancel();
    }
}
