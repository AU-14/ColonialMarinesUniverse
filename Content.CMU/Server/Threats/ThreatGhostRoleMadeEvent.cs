using Content.Server.Ghost.Roles.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Threats;

/// <summary>
///     Raised on a threat body nobody was assigned to, right after it became the generic threat ghost role.
///     Subscribers may change the role's text or give it a raffle; the job and mind roles are already set.
/// </summary>
[ByRefEvent]
public readonly record struct ThreatGhostRoleMadeEvent(ProtoId<JobPrototype> Job, bool Leader, GhostRoleComponent Role);
