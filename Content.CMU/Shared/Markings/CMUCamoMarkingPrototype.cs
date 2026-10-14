using Content.Shared._RMC14.Item;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Markings;

/// <summary>
///     A camo conforming underwear marking: its ID is the marking players pick, and on spawn it is swapped
///     for the standalone marking listed for the map's camouflage.
/// </summary>
[Prototype("cmuCamoMarking")]
public sealed partial class CMUCamoMarkingPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<CamouflageType, ProtoId<MarkingPrototype>> Variants = new();
}
