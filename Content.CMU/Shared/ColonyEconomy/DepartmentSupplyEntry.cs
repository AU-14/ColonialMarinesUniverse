using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

[DataDefinition]
public sealed partial class DepartmentSupplyEntry
{
    [DataField(required: true)]
    public EntProtoId Item;

    [DataField(required: true)]
    public int Cost;
}
