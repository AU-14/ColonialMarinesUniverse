using Content.Shared._RMC14.Requisitions.Components;

namespace Content.Server.CMU14.Requisitions;

/// <summary>
///     Raised broadcast when someone buys an order on a requisitions console. <see cref="Index"/> is where the order sits
///     in the elevator's queue, matching <see cref="CMURequisitionsCrateDeliveredEvent.Index"/> when it's delivered.
///     <see cref="ConsoleFaction"/> is the faction of the console it was bought on.
/// </summary>
[ByRefEvent]
public readonly record struct CMURequisitionsOrderBoughtEvent(
    Entity<RequisitionsElevatorComponent> Elevator,
    int Index,
    EntityUid Buyer,
    string? ConsoleFaction);

/// <summary>Raised broadcast for each crate a requisitions elevator delivers.</summary>
[ByRefEvent]
public readonly record struct CMURequisitionsCrateDeliveredEvent(
    Entity<RequisitionsElevatorComponent> Elevator,
    int Index,
    EntityUid Crate,
    string OrderName);
