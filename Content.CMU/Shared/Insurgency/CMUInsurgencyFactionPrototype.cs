using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Insurgency;

[Prototype("cmuInsurgencyFaction")]
public sealed partial class CMUInsurgencyFactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // Players' saved ballot exclusions use this stable number. Imported factions keep their original ID.
    [DataField(required: true)]
    public int BallotId;

    [DataField]
    public bool OpposesAll;

    [DataField(required: true)]
    public FactionDefinition Definition = default!;
}
