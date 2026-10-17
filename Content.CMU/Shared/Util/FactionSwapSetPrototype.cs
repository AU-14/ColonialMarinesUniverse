using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.util;

[Prototype]
public sealed partial class FactionSwapSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // Keys are Govfor-built prototype IDs, values their Opfor counterparts
    [DataField]
    public Dictionary<string, EntProtoId> Swaps { get; private set; } = new();
}
