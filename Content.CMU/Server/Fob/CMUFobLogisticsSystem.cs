using System.Linq;
using Content.Server._RMC14.Requisitions;
using Content.Server.CMU14.Ops.ForceOnForce;
using Content.Server.CMU14.Requisitions;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Requisitions;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Fob;

/// <summary>One free shipment and limited additional purchases per pilot round, through shipside requisitions.</summary>
public sealed partial class CMUFobLogisticsSystem : EntitySystem
{
    [Dependency] private FactionSwapSystem _factions = default!;
    [Dependency] private CMUFobMapSystem _maps = default!;
    [Dependency] private RequisitionsSystem _requisitions = default!;

    private static readonly EntProtoId SupplyCatalog = "CMUFobSupplyCatalog";

    private bool _issued;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMURequisitionsPreparingEvent>(OnPreparing);
        SubscribeLocalEvent<CMURequisitionsCatalogBuiltEvent>(OnCatalogBuilt);
        SubscribeLocalEvent<CMUFobSupplyAvailableEvent>(OnSupplyAvailable);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _issued = false);
    }

    private void OnSupplyAvailable(ref CMUFobSupplyAvailableEvent args)
    {
        var computers = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
            AddPurchases((uid, computer));
    }

    private void OnCatalogBuilt(ref CMURequisitionsCatalogBuiltEvent args)
    {
        if (TryComp<RequisitionsComputerComponent>(args.Computer, out var computer))
            AddPurchases((args.Computer, computer));
    }

    private void AddPurchases(Entity<RequisitionsComputerComponent> computer)
    {
        if (!_maps.PilotPrepared || computer.Comp.Faction != "govfor" || !IsFriendlyCarrier(computer))
            return;
        if (computer.Comp.Categories.SelectMany(c => c.Entries).Any(e => e.Crate.Id == "CMUFobWeaponryCrate"))
            return;
        _requisitions.AddRoundLimitedCatalog(computer, SupplyCatalog);
    }

    private void OnPreparing(ref CMURequisitionsPreparingEvent args)
    {
        var elevator = args.Elevator;
        if (_issued || !_maps.PilotPrepared || elevator.Comp.Faction != "govfor"
            || args.Capacity - elevator.Comp.Orders.Count < 3
            || Transform(elevator).GridUid is not { } grid || HasComp<DropshipComponent>(grid)
            || !IsFriendlyCarrier(elevator))
            return;

        // The round owns this grant, so replacing an elevator or returning to another LZ cannot reissue it.
        _issued = true;
        elevator.Comp.Orders.Add(new RequisitionsEntry { Crate = "CMUFobWeaponryCrate" });
        elevator.Comp.Orders.Add(new RequisitionsEntry { Crate = "CMUFobSupportCrate" });
        elevator.Comp.Orders.Add(new RequisitionsEntry { Crate = "CMUFobMonitoringCrate" });
        Dirty(elevator);
        Log.Info($"FOB physical supply allocation issued through {ToPrettyString(elevator)}.");
    }

    private bool IsFriendlyCarrier(EntityUid elevator)
    {
        var ships = EntityQueryEnumerator<ShipFactionComponent, TransformComponent>();
        while (ships.MoveNext(out var uid, out var ship, out var transform))
        {
            if (ship.Faction == "govfor" && !HasComp<DropshipComponent>(uid)
                && _factions.IsMarkerOnShipOrZLevel(uid, transform, Transform(elevator))) return true;
        }
        return false;
    }
}
