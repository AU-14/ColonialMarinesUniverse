using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats;

[Prototype]
public sealed partial class AntagJobBlacklistPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();
}