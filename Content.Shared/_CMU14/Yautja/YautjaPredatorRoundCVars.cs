using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._CMU14.Yautja;

[CVarDefs]
public sealed partial class YautjaPredatorRoundCVars : CVars
{
    public static readonly CVarDef<int> HunterSlots =
        CVarDef.Create("cmu.yautja.hunter_slots", 0, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> RandomEnabled =
        CVarDef.Create("cmu.yautja.random_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> RandomMinimumRounds =
        CVarDef.Create("cmu.yautja.random_minimum_rounds", 3, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> RandomMaximumRounds =
        CVarDef.Create("cmu.yautja.random_maximum_rounds", 5, CVar.SERVERONLY | CVar.ARCHIVE);
}
