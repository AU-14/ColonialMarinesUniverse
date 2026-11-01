using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.Yautja;

[CVarDefs]
public sealed partial class YautjaStrandedCVars : CVars
{
    // cmss13 pred_survivor_slots
    public static readonly CVarDef<int> Slots =
        CVarDef.Create("cmu.yautja.stranded_slots", 1, CVar.SERVERONLY | CVar.ARCHIVE);

    // rolled once at round start, hunt or no hunt. cmss13 only rolls pred_survivor_huntless_chance on
    // non-hunt rounds and always opens the slot on hunts, we don't want it guaranteed
    public static readonly CVarDef<float> Chance =
        CVarDef.Create("cmu.yautja.stranded_chance", 0.1f, CVar.SERVERONLY | CVar.ARCHIVE);
}
