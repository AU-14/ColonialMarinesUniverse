// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    // off: every commodity sells at its base price, no booms or gluts
    public static readonly CVarDef<bool> CMUMarketEnabled =
        CVarDef.Create("cmu.market_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    // base dollars of one commodity sold back to back that halve its price
    public static readonly CVarDef<float> CMUMarketSaturation =
        CVarDef.Create("cmu.market_saturation", 750f, CVar.SERVERONLY | CVar.ARCHIVE);

    // minutes for half of that selling pressure to wear off
    public static readonly CVarDef<float> CMUMarketRecoveryHalfLife =
        CVarDef.Create("cmu.market_recovery_half_life", 10f, CVar.SERVERONLY | CVar.ARCHIVE);

    // minutes between buyer events, picked at random between these
    public static readonly CVarDef<float> CMUMarketEventIntervalMin =
        CVarDef.Create("cmu.market_event_interval_min", 8f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUMarketEventIntervalMax =
        CVarDef.Create("cmu.market_event_interval_max", 14f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUMarketEventDuration =
        CVarDef.Create("cmu.market_event_duration", 12f, CVar.SERVERONLY | CVar.ARCHIVE);

    // price multipliers while a boom or glut runs
    public static readonly CVarDef<float> CMUMarketBoomMultiplier =
        CVarDef.Create("cmu.market_boom_multiplier", 1.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUMarketGlutMultiplier =
        CVarDef.Create("cmu.market_glut_multiplier", 0.7f, CVar.SERVERONLY | CVar.ARCHIVE);

    // chance an event is a boom rather than a glut
    public static readonly CVarDef<float> CMUMarketBoomChance =
        CVarDef.Create("cmu.market_boom_chance", 0.7f, CVar.SERVERONLY | CVar.ARCHIVE);
}
