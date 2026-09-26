// ReSharper disable once CheckNamespace
namespace Content.Shared.Maps;

public sealed partial class ContentTileDefinition
{
    /// <summary>Visual water depth in pixels, for shoreline tiles without a water entity.</summary>
    [DataField]
    public float RMCWaterDepth;
}
