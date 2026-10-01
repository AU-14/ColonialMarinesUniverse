using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Player;

namespace Content.Server.Ghost.Roles.Events;

[ByRefEvent]
public record struct GhostRoleRequestAttemptEvent(
    ICommonSession Player,
    EntityUid Role,
    GhostRoleComponent Component,
    bool Cancelled = false,
    bool ExplicitRequest = false); // CMU14: distinguish requests from passive raffle eligibility checks.
