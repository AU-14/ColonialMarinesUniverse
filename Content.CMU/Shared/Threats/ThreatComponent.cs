using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats;

[RegisterComponent]
public sealed partial class ThreatComponent : Component
{
    /// <summary>
    /// The leader/member assignment used by objectives, independent of the body's caste job or its current mind.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? ObjectiveJob;
}
