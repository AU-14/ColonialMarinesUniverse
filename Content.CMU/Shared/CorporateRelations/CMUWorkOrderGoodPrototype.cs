using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.CorporateRelations;

/// <summary>
///     Something Weyland-Yutani can put a work order out for. Deliveries are matched by
///     <see cref="Tag"/> at the colony's submission storage.
/// </summary>
[Prototype("cmuWorkOrderGood")]
public sealed partial class CMUWorkOrderGoodPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<TagPrototype> Tag;

    /// <summary>
    ///     Reference value of one unit. The premium WY offers is a percentage of this.
    /// </summary>
    [DataField(required: true)]
    public int UnitValue;

    [DataField(required: true)]
    public int MaxQuantity;
}
