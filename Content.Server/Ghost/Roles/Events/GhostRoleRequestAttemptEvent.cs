using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Player;

namespace Content.Server.Ghost.Roles.Events;

[ByRefEvent]
public record struct GhostRoleRequestAttemptEvent(
    ICommonSession Player,
    EntityUid Role,
    GhostRoleComponent Component,
    bool Cancelled = false);
