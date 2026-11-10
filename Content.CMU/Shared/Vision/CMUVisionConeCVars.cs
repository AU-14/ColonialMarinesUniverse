using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.Vision;

[CVarDefs]
public sealed partial class CMUVisionConeCVars : CVars
{
    /// <summary>
    /// Global switch for vision cones, set by the server and replicated to clients.
    /// When disabled, nobody has their view restricted.
    /// </summary>
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("cmu.vision_cone.enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
