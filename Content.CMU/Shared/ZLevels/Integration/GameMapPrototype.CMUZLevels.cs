using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Maps;

public sealed partial class GameMapPrototype
{
    /// <summary>
    /// Additional maps loaded below <see cref="MapPath"/>, ordered from the lowest depth to -1.
    /// </summary>
    [DataField]
    public List<ResPath> MapsBelow = new();

    /// <summary>
    /// Additional maps loaded above <see cref="MapPath"/>, ordered from depth 1 upward.
    /// </summary>
    [DataField]
    public List<ResPath> MapsAbove = new();

    /// <summary>
    /// Components applied to every map entity in the Z-network.
    /// </summary>
    [DataField]
    public ComponentRegistry ZLevelsComponentOverrides = new();
}
