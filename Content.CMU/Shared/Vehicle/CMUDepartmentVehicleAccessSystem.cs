using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Vehicle;

public sealed partial class CMUDepartmentVehicleAccessSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ColonyDepartmentVehicleComponent, VehicleCanRunEvent>(OnCanRun);
        if (_net.IsClient)
            return;

        SubscribeLocalEvent<ColonyDepartmentVehicleComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<ColonyDepartmentVehicleComponent, AccessReaderConfigurationChangedEvent>(OnReaderChanged);
        SubscribeLocalEvent<VehicleOperatorComponent, EntInsertedIntoContainerMessage>(OnDriverInventoryChanged);
        SubscribeLocalEvent<VehicleOperatorComponent, EntRemovedFromContainerMessage>(OnDriverInventoryChanged);
    }

    private void OnDriverInventoryChanged(EntityUid uid, VehicleOperatorComponent driver, ContainerModifiedMessage args)
    {
        if (TryComp<ColonyDepartmentVehicleComponent>(driver.Vehicle, out var vehicle))
            vehicle.NextAccessCheck = TimeSpan.Zero;
    }

    private void OnReaderChanged(EntityUid uid, ColonyDepartmentVehicleComponent vehicle, AccessReaderConfigurationChangedEvent args)
    {
        vehicle.NextAccessCheck = TimeSpan.Zero;
    }

    private void OnOperatorSet(Entity<ColonyDepartmentVehicleComponent> vehicle, ref VehicleOperatorSetEvent args)
    {
        UpdateAccess(vehicle, args.NewOperator);
    }

    private void OnCanRun(Entity<ColonyDepartmentVehicleComponent> vehicle, ref VehicleCanRunEvent args)
    {
        if (args.Vehicle.Comp.Operator is not { } driver)
            return;

        if (_net.IsServer && (vehicle.Comp.LastCheckedDriver != driver || vehicle.Comp.NextAccessCheck <= _timing.CurTime))
            UpdateAccess(vehicle, driver);

        if (vehicle.Comp.AuthorizedDriver != driver)
            args.CanRun = false;
    }

    private void UpdateAccess(Entity<ColonyDepartmentVehicleComponent> vehicle, EntityUid? driver)
    {
        vehicle.Comp.LastCheckedDriver = driver;
        // A card can also be reprogrammed in place without an inventory change.
        vehicle.Comp.NextAccessCheck = _timing.CurTime + TimeSpan.FromSeconds(1);
        var authorized = driver;
        if (driver is { } user && TryComp<AccessReaderComponent>(vehicle, out var reader) && reader.Enabled)
        {
            var items = _access.FindPotentialAccessItems(user);
            var tags = _access.FindAccessTags(user, items);
            _access.FindStationRecordKeys(user, out var keys, items);
            if (!_access.IsAllowed(tags, keys, vehicle, reader))
                authorized = null;
        }

        if (vehicle.Comp.AuthorizedDriver == authorized)
            return;

        vehicle.Comp.AuthorizedDriver = authorized;
        Dirty(vehicle);
    }
}
