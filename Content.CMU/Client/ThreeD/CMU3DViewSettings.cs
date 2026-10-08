using Robust.Shared.Configuration;

namespace Content.Client.CMU14.ThreeD;

[CVarDefs]
public sealed class CMU3DViewSettings
{
    public const float DefaultFov = 75;
    public const float DefaultDistance = 24;
    public const int DefaultPixelBudget = 1920 * 1080;
    public static readonly CVarDef<bool> Enabled = CVarDef.Create("cmu.3d.enabled", false, CVar.CLIENTONLY);
    public static readonly CVarDef<float> Fov = CVarDef.Create("cmu.3d.fov", DefaultFov, CVar.CLIENTONLY | CVar.ARCHIVE);
    public static readonly CVarDef<float> Distance = CVarDef.Create("cmu.3d.distance", DefaultDistance, CVar.CLIENTONLY | CVar.ARCHIVE);
    public static readonly CVarDef<float> Resolution = CVarDef.Create("cmu.3d.resolution", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
    public static readonly CVarDef<int> PixelBudget = CVarDef.Create("cmu.3d.max_pixels", DefaultPixelBudget, CVar.CLIENTONLY | CVar.ARCHIVE);
    public static readonly CVarDef<float> Brightness = CVarDef.Create("cmu.3d.brightness", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static float Clamp(float value, float min, float max, float fallback) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
