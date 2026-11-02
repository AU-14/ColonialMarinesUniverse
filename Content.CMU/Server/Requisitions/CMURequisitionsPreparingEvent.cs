using Content.Shared._RMC14.Requisitions.Components;

namespace Content.Server.CMU14.Requisitions;

/// <summary>Allows round-owned allocations to join a shipment before its cargo is prepared.</summary>
[ByRefEvent]
public readonly record struct CMURequisitionsPreparingEvent(Entity<RequisitionsElevatorComponent> Elevator, int Capacity);
