using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Roles;

[RegisterComponent]
public sealed partial class RoundJobProfileAppliedComponent : Component;

/// <summary>
/// Raised on an entity once <see cref="RoundJobProfileSystem.ApplyJobProfile"/> has applied a job's round profile.
/// </summary>
[ByRefEvent]
public readonly record struct CMURoundJobProfileAppliedEvent(Content.Shared.Roles.JobPrototype Job);
