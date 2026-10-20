namespace Content.Shared.CMU14.Requisitions;

/// <summary>
/// Raised on an ASRS computer after its catalog has been assembled at map init, so CMU systems
/// can append conditional entries without being overwritten by the platoon catalog swap.
/// </summary>
[ByRefEvent]
public readonly record struct CMURequisitionsCatalogBuiltEvent;
