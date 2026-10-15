using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Chance for each installed CAS hardpoint to fault during the first fire-control boot.
    /// </summary>
    public static readonly CVarDef<float> DropshipBootFaultChance =
        CVarDef.Create("cmu.dropship.boot_fault_chance", 0.02f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether fire control re-runs its boot test after every landing.
    /// </summary>
    public static readonly CVarDef<bool> DropshipLandingReboot =
        CVarDef.Create("cmu.dropship.landing_reboot", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Base chance for a hardpoint to fault during a landing reboot.
    /// </summary>
    public static readonly CVarDef<float> DropshipLandingFaultChance =
        CVarDef.Create("cmu.dropship.landing_fault_chance", 0.005f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Chance added for every landing since the hardpoint was last serviced.
    /// </summary>
    public static readonly CVarDef<float> DropshipLandingFaultStep =
        CVarDef.Create("cmu.dropship.landing_fault_step", 0.002f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Multiplier added to a hardpoint's landing fault chance for each firing engagement since service.
    /// </summary>
    public static readonly CVarDef<float> DropshipFaultUseMultiplier =
        CVarDef.Create("cmu.dropship.fault_use_multiplier", 0.01f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Multiplier applied to landing fault chances when the dropship crash lands.
    /// </summary>
    public static readonly CVarDef<float> DropshipCrashFaultMultiplier =
        CVarDef.Create("cmu.dropship.crash_fault_multiplier", 3f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Upper bound for any single landing fault roll.
    /// </summary>
    public static readonly CVarDef<float> DropshipFaultMaxChance =
        CVarDef.Create("cmu.dropship.fault_max_chance", 0.75f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Base chance for a CAS weapon to jam after a shot. Scaled by the hardpoint's firing engagements since service.
    /// </summary>
    public static readonly CVarDef<float> DropshipJamChance =
        CVarDef.Create("cmu.dropship.jam_chance", 0.002f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
