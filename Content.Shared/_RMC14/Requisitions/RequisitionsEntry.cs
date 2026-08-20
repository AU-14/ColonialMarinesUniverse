using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Requisitions;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class RequisitionsEntry
{
    [NonSerialized] public string? DeptOrderedBy;
    [NonSerialized] public string? DeptReason;
    [NonSerialized] public string? DeptDeliverTo;
    [NonSerialized] public string? DeptAccessLevel;
    [NonSerialized] public string? DeptName;
    [DataField]
    public string? Name;

    [DataField(required: true)]
    public int Cost;

    [DataField(required: true)]
    public EntProtoId Crate;

    [DataField]
    public List<EntProtoId> Entities = new();

    /// <summary>
    /// Maximum stock for limited ASRS entries. Entries with a value of 0 or lower are unlimited.
    /// </summary> CMU14
    [DataField]
    public int MaxStock;

    /// <summary>
    /// Starting stock for limited ASRS entries. A negative value starts the entry at <see cref="MaxStock"/>.
    /// </summary> CMU14
    [DataField]
    public int StartingStock = -1;

    /// <summary>
    /// How long it takes for a limited entry to restock.
    /// </summary> CMU14
    [DataField]
    public TimeSpan StockReplenishDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How many units are restored each restock tick.
    /// </summary> CMU14
    [DataField]
    public int StockReplenishAmount = 1;
}
