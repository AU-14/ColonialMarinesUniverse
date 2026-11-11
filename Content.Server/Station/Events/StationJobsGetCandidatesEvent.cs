using Content.Shared.Preferences; // CMU14
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Events;

// CMU14: requirements must use the same character profile as the assignment being evaluated.
[ByRefEvent]
public readonly record struct StationJobsGetCandidatesEvent(NetUserId Player, List<ProtoId<JobPrototype>> Jobs, HumanoidCharacterProfile Profile);
