using System.Collections.Generic;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round;

/// <summary>
/// Restores jobs on the planet station with the counts the map declared in
/// StationJobs availableJobs, e.g. the colonist population after RemoveAllJobs.
/// </summary>
[RegisterComponent]
public sealed partial class AddColonistRuleComponent : Component
{
    /// <summary>
    /// Optional filter; omitted means every job the planet map declares.
    /// </summary>
    [DataField("jobs")]
    public HashSet<ProtoId<JobPrototype>>? Jobs { get; set; }
}
