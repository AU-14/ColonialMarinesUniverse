using Content.Shared._RMC14.Construction.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Construction;

/// <summary>
/// Raised broadcast by <c>RMCConstructionSystem.Build</c> after the placement checks and before the cost check and
/// do-after. A handler sets <see cref="Cancelled"/> to refuse the build; <see cref="Popup"/>, if set, is shown to the user.
/// </summary>
[ByRefEvent]
public record struct CMUConstructionBuildAttemptEvent(
    EntityUid User,
    EntityUid Item,
    ProtoId<RMCConstructionPrototype> Recipe,
    EntityCoordinates Coordinates,
    bool Cancelled = false,
    string? Popup = null);
