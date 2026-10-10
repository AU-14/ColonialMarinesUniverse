using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats;

[Prototype]
public sealed partial class AntagJobBlacklistPrototype : IPrototype
{
    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();

    /// <summary>
    /// Excludes every job assigned to these round sides, including faction-specific job variants.
    /// </summary>
    [DataField]
    public HashSet<RoundJobSide> RoundSides = new();

    [IdDataField]
    public string ID { get; private set; } = default!;
}
