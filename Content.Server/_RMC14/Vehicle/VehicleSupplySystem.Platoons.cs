using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.CMU14.util;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Vehicle;

// CMU14: round-wide supply allowances belong to the side, not individual lifts or vehicle prototypes.
public sealed partial class VehicleSupplySystem
{
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;

    private readonly record struct IssuedVehicle(EntProtoId Prototype, string? Group);
    private readonly Dictionary<string, List<IssuedVehicle>> _issuedVehicles = new();

    private void OnSupplyRoundRestart(RoundRestartCleanupEvent args) => _issuedVehicles.Clear();

    public string? GetSupplySide(EntityUid uid)
    {
        if (TryComp(uid, out VehicleSupplyConsoleComponent? console) && IsSupplySide(console.Faction))
            return Normalize(console.Faction!);
        if (_dropships.TryGetGridFaction(uid, out var faction) && IsSupplySide(faction))
            return Normalize(faction);

        // Groundside depots share their nearest requisitions console's side.
        var position = _transform.GetMapCoordinates(uid);
        var nearest = float.MaxValue;
        string? side = null;
        var query = EntityQueryEnumerator<RequisitionsComputerComponent, TransformComponent>();
        while (query.MoveNext(out var computer, out var req, out var xform))
        {
            if (!IsSupplySide(req.Faction) || xform.MapID != position.MapId)
                continue;
            var distance = (_transform.GetMapCoordinates(computer, xform).Position - position.Position).LengthSquared();
            if (distance >= nearest)
                continue;
            nearest = distance;
            side = Normalize(req.Faction!);
        }
        return side;
    }

    private static bool IsSupplySide(string? side) =>
        string.Equals(side, "govfor", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(side, "opfor", StringComparison.OrdinalIgnoreCase);

    private PlatoonPrototype? GetSupplyPlatoon(string? side) => side switch
    {
        "govfor" => _platoons.SelectedGovforPlatoon,
        "opfor" => _platoons.SelectedOpforPlatoon,
        _ => null,
    };

    public List<VehicleSupplyEntry> GetCatalog(EntityUid uid, VehicleSupplyConsoleComponent console)
    {
        var platoon = GetSupplyPlatoon(GetSupplySide(uid));
        return platoon == null
            ? new List<VehicleSupplyEntry>()
            : console.Vehicles.Where(entry => platoon.VehicleSupplyCatalog.Contains(entry.Vehicle)).ToList();
    }

    private List<IssuedVehicle> GetIssued(string side)
    {
        if (!_issuedVehicles.TryGetValue(side, out var issued))
            _issuedVehicles[side] = issued = new List<IssuedVehicle>();
        return issued;
    }

    private bool CanIssueVehicle(string? side, VehicleSupplyEntry entry, EntityUid? ignoreLift = null)
    {
        if (side == null || GetSupplyPlatoon(side) is not { } platoon ||
            !platoon.VehicleSupplyCatalog.Contains(entry.Vehicle))
            return false;

        var issued = GetIssued(side);
        var count = issued.Count;
        var group = GetEntryGroupKey(entry);
        var groupCount = issued.Count(vehicle => vehicle.Group == group);
        var query = EntityQueryEnumerator<VehicleSupplyLiftComponent>();
        while (query.MoveNext(out var uid, out var lift))
        {
            if (uid == ignoreLift || lift.PendingSupplySide != side ||
                string.IsNullOrEmpty(lift.PendingVehicle) || lift.PendingVehicleEntity != null)
                continue;
            count++;
            if (lift.PendingVehicleGroup == group)
                groupCount++;
        }
        return count < platoon.MaxSuppliedVehicles &&
            (group != "vehicle-tank" || groupCount < platoon.MaxSuppliedTanks) &&
            (group != "vehicle-vtol" || groupCount < platoon.MaxSuppliedVtols);
    }

    private bool CanSelectVehicle(Entity<VehicleSupplyLiftComponent> lift, VehicleSupplyEntry entry, string? side)
    {
        var key = Normalize(entry.Vehicle.Id);
        return GetStoredCount(lift.Comp, key) > 0 &&
            (TryGetStoredEntity(lift.Comp, key, 0, out _) || CanIssueVehicle(side, entry));
    }

    private void CancelPendingVehicle(Entity<VehicleSupplyLiftComponent> lift)
    {
        if (lift.Comp.PendingVehicleEntity is { } stored && Exists(stored))
        {
            var key = Normalize(lift.Comp.PendingVehicle);
            AddStored(lift.Comp, key);
            AddStoredEntity(lift.Comp, key, stored);
        }
        lift.Comp.PendingVehicle = string.Empty;
        lift.Comp.PendingVehicleEntity = null;
        lift.Comp.PendingVehicleGroup = string.Empty;
        lift.Comp.PendingSupplySide = null;
        lift.Comp.PendingSupplyConsole = null;
        ClearPendingLoadout(lift.Comp);
    }

    public void ReapplySupplyCatalogs()
    {
        _hardpointsByVehicleCache.Clear();
        var consoles = EntityQueryEnumerator<VehicleSupplyConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            console.SelectedVehicle = string.Empty;
            console.SelectedLoadouts.Clear();
            BackfillLiftFromConsole((uid, console));
        }
        SendConsoleStateAll();
        UpdateVendorSectionsAll();
    }
}
