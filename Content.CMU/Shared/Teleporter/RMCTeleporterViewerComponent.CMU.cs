using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Teleporter;

public sealed partial class RMCTeleporterViewerComponent
{
    /// <summary>
    /// Whether entering this viewer projects its matching viewer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ProjectionEnabled = true;

    /// <summary>
    /// Whether anchored sprite entities inside the remote footprint are projected.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ProjectAnchored;

    /// <summary>
    /// Whether grid tiles inside the remote footprint are projected.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ProjectTiles;

    /// <summary>
    /// Bottom-to-top rows describing which tiles in the viewer fixture are projected.
    /// Rows are separated by slashes, with '#' for projected tiles and '.' for omitted tiles.
    /// An empty value projects the entire fixture.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string ProjectionTileMask = string.Empty;
}
