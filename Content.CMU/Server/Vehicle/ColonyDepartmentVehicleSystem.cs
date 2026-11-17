using Content.Server.Popups;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Vehicle;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle;

namespace Content.Server.CMU14.Vehicle;

/// <summary>Enforces both cabin entry and driving access on colony department vehicles.</summary>
public sealed class ColonyDepartmentVehicleSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyDepartmentVehicleComponent, VehicleEntryAttemptEvent>(OnEntryAttempt);
        SubscribeLocalEvent<ColonyDepartmentVehicleComponent, VehicleCanRunEvent>(OnCanRun);
    }

    private void OnEntryAttempt(Entity<ColonyDepartmentVehicleComponent> vehicle, ref VehicleEntryAttemptEvent args)
    {
        if (_access.IsAllowed(args.User, vehicle))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("cmu-colony-department-truck-access-denied"), vehicle, args.User);
    }

    private void OnCanRun(Entity<ColonyDepartmentVehicleComponent> vehicle, ref VehicleCanRunEvent args)
    {
        if (args.Vehicle.Comp.Operator is { } driver && !_access.IsAllowed(driver, vehicle))
            args.CanRun = false;
    }
}
