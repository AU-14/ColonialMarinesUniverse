using System.Collections.Generic;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round;

/// <summary>
/// Restores a fixed colonist roster on the planet station after RemoveAllJobs.
/// </summary>
[RegisterComponent]
public sealed partial class AddColonistRuleComponent : Component
{
    /// <summary>Exact slot count restored per job.</summary>
    [DataField("jobs")]
    public Dictionary<ProtoId<JobPrototype>, int> Jobs { get; set; } = new();

    /// <summary>
    /// One slot goes to one randomly picked job from this list, e.g. the round's
    /// ambassador allegiance. Only map-declared jobs are picked.
    /// </summary>
    [DataField("randomJobs")]
    public List<ProtoId<JobPrototype>> RandomJobs { get; set; } = new();
}
