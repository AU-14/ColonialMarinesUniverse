using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Marines.Roles.Chevrons;

[DataDefinition]
public sealed partial class ChevronDefinition
{
    [DataField(required: true)]
    public EntProtoId Entity { get; set; }

    [DataField]
    public HashSet<JobRequirement>? Requirements { get; set; }
}