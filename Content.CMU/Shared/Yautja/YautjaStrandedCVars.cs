using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.Yautja;

[CVarDefs]
public sealed partial class YautjaStrandedCVars : CVars
{
    // cmss13 pred_survivor_slots
    public static readonly CVarDef<int> Slots =
        CVarDef.Create("cmu.yautja.stranded_slots", 1, CVar.SERVERONLY | CVar.ARCHIVE);

    // cmss13 pred_survivor_huntless_chance, rolled once at round start when there's no hunt
    public static readonly CVarDef<float> HuntlessChance =
        CVarDef.Create("cmu.yautja.stranded_huntless_chance", 0.1f, CVar.SERVERONLY | CVar.ARCHIVE);
}
