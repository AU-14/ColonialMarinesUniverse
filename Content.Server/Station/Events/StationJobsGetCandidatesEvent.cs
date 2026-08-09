using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Events;

// CMU14: This event is the filter boundary for the per-assignment eligibility snapshot.
/// <summary>
/// Filters a player's preferred jobs when round-start eligibility is captured.
/// Raised once per non-forced player for each <c>AssignJobs</c> call; handlers should only remove jobs.
/// </summary>
[ByRefEvent]
public readonly record struct StationJobsGetCandidatesEvent(NetUserId Player, List<ProtoId<JobPrototype>> Jobs);
