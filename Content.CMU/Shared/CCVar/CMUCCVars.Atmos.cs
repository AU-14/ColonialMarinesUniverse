using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// How many generations ordinary tile fires creep outward from where they were lit.
    /// Each generation spawns one adjacent fire that gets one less, so reach is depth - 1
    /// tiles. 0 disables creeping for fires that do not carry their own depth in YAML.
    /// </summary>
    public static readonly CVarDef<int> CMUFireSpreadDepth =
        CVarDef.Create("cmu.fire_spread_depth", 2, CVar.SERVERONLY);

    /// <summary>
    /// Whether sky-exposed tiles on planet maps relax back toward the map's
    /// immutable MapAtmosphere mixture. Space maps (space: true, or no
    /// MapAtmosphere) are never affected.
    /// </summary>
    public static readonly CVarDef<bool> CMUOutdoorAtmosRelax =
        CVarDef.Create("cmu.atmos.outdoor_relax", true, CVar.SERVERONLY);

    /// <summary>
    /// Fraction per pass (passes run every 0.5 s) that outdoor tile air moves
    /// toward the planet baseline. 0.05 corrects roughly 63% in 10 s and 95%
    /// in 30 s. Values outside 0..1 are clamped at read.
    /// </summary>
    public static readonly CVarDef<float> CMUOutdoorAtmosRelaxRate =
        CVarDef.Create("cmu.atmos.outdoor_relax_rate", 0.05f, CVar.SERVERONLY);

    /// <summary>
    /// Master switch for the day-night temperature driver
    /// (CMUDayNightAtmosphereComponent on the map entity). Maps without the
    /// component are never affected.
    /// </summary>
    public static readonly CVarDef<bool> CMUDayNightAtmosTemp =
        CVarDef.Create("cmu.atmos.day_night_temp", true, CVar.SERVERONLY);

    /// <summary>
    /// Client preference: show temperatures in Fahrenheit instead of Celsius.
    /// Display only, simulation always runs in Kelvin.
    /// </summary>
    public static readonly CVarDef<bool> CMUTemperatureFahrenheit =
        CVarDef.Create("cmu.temperature.fahrenheit", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
