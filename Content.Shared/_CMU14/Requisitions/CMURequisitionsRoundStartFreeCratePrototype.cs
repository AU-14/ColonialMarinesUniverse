using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Requisitions;

[Prototype("cmuRequisitionsRoundStartFreeCrate")]
public sealed partial class CMURequisitionsRoundStartFreeCratePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Faction = string.Empty;

    [DataField]
    public List<string> Gamemodes = new();

    [DataField(required: true)]
    public List<EntProtoId> Crates = new();
}
