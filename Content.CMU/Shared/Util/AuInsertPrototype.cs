using Robust.Shared.Prototypes;
using Robust.Shared.Utility;


namespace Content.Shared.AU14.util;
[Prototype]
public sealed partial class AuInsertPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField("Weight")]
    public float Weight { get; private set; }

    [DataField("markerID")]
    public string MarkerID { get; private set; } = "genericinsert";

    [DataField("resPath")]
    public string ResPath { get; private set; } = "";
}
