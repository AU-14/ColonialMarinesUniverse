using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Marines.Roles.Chevrons;

[DataDefinition]
public sealed partial class ChevronDefinition
{
    [DataField(required: true)]
    public EntProtoId Entity { get; set; }

    [DataField]
    public List<JobRequirement>? Requirements { get; set; }
}