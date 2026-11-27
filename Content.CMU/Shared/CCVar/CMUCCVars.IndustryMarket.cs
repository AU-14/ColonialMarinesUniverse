// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    // off: industry exports always pay their full ASRS price
    public static readonly CVarDef<bool> CMUIndustryMarketEnabled =
        CVarDef.Create("cmu.industry_market_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    // minutes for what a faction has sold to count half as much against it
    public static readonly CVarDef<float> CMUIndustryMarketHalfLife =
        CVarDef.Create("cmu.industry_market_half_life", 10f, CVar.SERVERONLY | CVar.ARCHIVE);

    // the rest are in credits of recent sales (full price). "free" is how much sells before prices start to drop,
    // "slope" is how much more halves them. at steady selling a faction can never earn more than about
    // 4.16 * (free + slope) an hour from one product (item) or from everything together (total), whatever it sells.
    // colony, corporate and anyone else
    public static readonly CVarDef<float> CMUIndustryMarketItemFree =
        CVarDef.Create("cmu.industry_market_item_free", 1920f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUIndustryMarketItemSlope =
        CVarDef.Create("cmu.industry_market_item_slope", 1440f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUIndustryMarketTotalFree =
        CVarDef.Create("cmu.industry_market_total_free", 2900f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUIndustryMarketTotalSlope =
        CVarDef.Create("cmu.industry_market_total_slope", 3600f, CVar.SERVERONLY | CVar.ARCHIVE);

    // govfor and opfor: industry tops up req, it shouldn't replace it
    public static readonly CVarDef<float> CMUIndustryMarketMilitaryItemFree =
        CVarDef.Create("cmu.industry_market_military_item_free", 960f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUIndustryMarketMilitaryItemSlope =
        CVarDef.Create("cmu.industry_market_military_item_slope", 960f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUIndustryMarketMilitaryTotalFree =
        CVarDef.Create("cmu.industry_market_military_total_free", 1200f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMUIndustryMarketMilitaryTotalSlope =
        CVarDef.Create("cmu.industry_market_military_total_slope", 1440f, CVar.SERVERONLY | CVar.ARCHIVE);
}
