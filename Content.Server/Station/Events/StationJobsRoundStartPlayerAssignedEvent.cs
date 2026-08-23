using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Events;

/// <summary>
/// Raised by round-start job assignment after a player has consumed a station job slot.
/// </summary>
public readonly record struct StationJobsRoundStartPlayerAssignedEvent(
    NetUserId Player,
    ProtoId<JobPrototype> Job,
    EntityUid Station);
