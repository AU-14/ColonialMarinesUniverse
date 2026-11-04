using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Enables the camera map UI. Disabled until map geometry is distributed without remote-grid PVS subscriptions.
    /// </summary>
    public static readonly CVarDef<bool> CMUCameraMapEnabled =
        CVarDef.Create("cmu.camera.map_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Enables runtime camera-network editing. Disabled until editing is scoped and separately authorized.
    /// </summary>
    public static readonly CVarDef<bool> CMUCameraEditorEnabled =
        CVarDef.Create("cmu.camera.editor_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);
}
