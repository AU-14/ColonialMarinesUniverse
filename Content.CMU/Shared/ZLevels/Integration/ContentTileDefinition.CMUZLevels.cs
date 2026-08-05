namespace Content.Shared.Maps;

public sealed partial class ContentTileDefinition
{
    /// <summary>
    /// Whether this tile allows projected light and visibility through a Multi-Z opening.
    /// </summary>
    [DataField]
    public bool Transparent;
}
