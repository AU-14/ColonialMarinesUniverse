using System.Collections.Generic;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Round;

/// <summary>
/// Restores a fixed colonist roster on the planet station after RemoveAllJobs.
/// Lives in Shared so the lobby role editor can offer exactly the FoF colonist roster.
/// </summary>
[RegisterComponent]
public sealed partial class AddColonistRuleComponent : Component
{
    /// <summary>Exact slot count restored per job.</summary>
    [DataField("jobs")]
    public Dictionary<ProtoId<JobPrototype>, int> Jobs { get; set; } = new();

    /// <summary>
    /// One slot goes to one randomly picked job from this list, e.g. the round's
    /// ambassador allegiance.
    /// </summary>
    [DataField("randomJobs")]
    public List<ProtoId<JobPrototype>> RandomJobs { get; set; } = new();

    /// <summary>
    /// Given to spawned colonists whose ears carry no headset, so every
    /// roster job can reach the colony channel.
    /// </summary>
    [DataField("headset")]
    public EntProtoId Headset = "CMHeadsetColony";
}
