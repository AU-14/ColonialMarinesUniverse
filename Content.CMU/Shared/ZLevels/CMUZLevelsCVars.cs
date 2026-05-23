using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._CMU14.ZLevels;

[CVarDefs]
public sealed partial class CMUZLevelsCVars : CVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("cmu.zlevels.enabled", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> RenderEnabled =
        CVarDef.Create("cmu.zlevels.render_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> MaxRenderDepth =
        CVarDef.Create("cmu.zlevels.max_render_depth", 8, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> BlurEnabled =
        CVarDef.Create("cmu.zlevels.blur_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> WeatherLowerLayers =
        CVarDef.Create("cmu.zlevels.weather_lower_layers", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> CrossZAudio =
        CVarDef.Create("cmu.zlevels.cross_z_audio", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> VisibleEntityIndicators =
        CVarDef.Create("cmu.zlevels.visible_entity_indicators", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ProbeUpdateHz =
        CVarDef.Create("cmu.zlevels.probe_update_hz", 4.0f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<int> MaxViewProbesPerPlayer =
        CVarDef.Create("cmu.zlevels.max_view_probes_per_player", 5, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<float> MinProbePvsScale =
        CVarDef.Create("cmu.zlevels.min_probe_pvs_scale", 1.0f, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<int> MaxFallsPerTick =
        CVarDef.Create("cmu.zlevels.max_falls_per_tick", 64, CVar.SERVER);

    public static readonly CVarDef<float> TransitionBudgetMs =
        CVarDef.Create("cmu.zlevels.transition_budget_ms", 1.0f, CVar.SERVER);

    public static readonly CVarDef<bool> CullOccludedDynamicSprites =
        CVarDef.Create("cmu.zlevels.cull_occluded_dynamic_sprites", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> MaxOpeningRectsPerPass =
        CVarDef.Create("cmu.zlevels.max_opening_rects_per_pass", 512, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ProjectedLightingEnabled =
        CVarDef.Create("cmu.zlevels.projected_lighting", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> MaxProjectedLightsPerLevel =
        CVarDef.Create("cmu.zlevels.projected_lighting_max_per_level", 16, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ProjectedLightAttenuationPerDepth =
        CVarDef.Create("cmu.zlevels.projected_lighting_atten_depth", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ProjectedLightAttenuationPerTile =
        CVarDef.Create("cmu.zlevels.projected_lighting_atten_tile", 0.1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ProjectedLightMaxRadius =
        CVarDef.Create("cmu.zlevels.projected_lighting_max_radius", 12f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ProjectedLightRadiusScale =
        CVarDef.Create("cmu.zlevels.projected_lighting_radius_scale", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ProjectedLightMinEnergy =
        CVarDef.Create("cmu.zlevels.projected_lighting_min_energy", 0.05f, CVar.CLIENTONLY | CVar.ARCHIVE);

}
