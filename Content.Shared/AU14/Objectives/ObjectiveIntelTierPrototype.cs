using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
namespace Content.Shared.AU14.Objectives;


[Prototype]
public sealed partial class ObjectiveIntelTierPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; } = default!;




    [DataField]

    public bool DisplayOnTacMap { get; set; } = false;
    // Dependent on obj, kill will show markedforkill, fetch will show fetchitems, capture will show the location of the point

    [DataField("title")]
    public string TitleText { get; set; } = string.Empty;
    // if empty, will not override the title from the obj/last tier in the console. If set, will override it.
    // if set to Unknown, objective will be considered unknown
    [DataField("description")]
    public string DescriptionText { get; set; } = string.Empty;
    // same behavior as title

    [DataField]
    public bool ListCoords { get; set; } = false;
    // Similar to displayontacmap but lists relevant coordinates instead of displaying

    [DataField("costToUnlock")]
    public double  CostToUnlock { get; set; } = 0.5;
    // costin intel points to unlock this tier


}
