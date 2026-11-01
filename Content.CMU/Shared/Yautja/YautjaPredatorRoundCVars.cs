using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.Yautja;

[CVarDefs]
public sealed partial class YautjaPredatorRoundCVars : CVars
{
    public static readonly CVarDef<int> HunterSlots =
        CVarDef.Create("cmu.yautja.hunter_slots", 0, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> RandomEnabled =
        CVarDef.Create("cmu.yautja.random_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    // per-round dice, only Insurgency and Distress Signal ever roll. Insurgency is meant to be twice as rare
    public static readonly CVarDef<float> InsurgencyChance =
        CVarDef.Create("cmu.yautja.insurgency_chance", 1f / 6f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> DistressSignalChance =
        CVarDef.Create("cmu.yautja.distress_signal_chance", 1f / 3f, CVar.SERVERONLY | CVar.ARCHIVE);
}
