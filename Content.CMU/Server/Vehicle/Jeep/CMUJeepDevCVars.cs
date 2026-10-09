using Robust.Shared.Configuration;

namespace Content.Server.CMU14.Vehicle.Jeep;

/// <summary>
/// Jeep test server settings. All off by default; the jeep test config turns them on. See
/// <see cref="CMUJeepDevKitSystem"/>.
/// </summary>
[CVarDefs]
public sealed class CMUJeepDevCVars
{
    /// <summary>
    /// On a server without a lobby, parks the three jeeps and a test kit on open ground next to each
    /// player who spawns, once per player per round.
    /// </summary>
    public static readonly CVarDef<bool> DevKit = CVarDef.Create("cmu.jeep.dev_kit", false, CVar.SERVERONLY);

    /// <summary>
    /// Job given to joining players when their station offers it, so a test account spawns as a
    /// person whatever its saved preferences say. Empty to leave jobs alone.
    /// </summary>
    public static readonly CVarDef<string> DevJob = CVarDef.Create("cmu.jeep.dev_job", "", CVar.SERVERONLY);

    /// <summary>
    /// Sets the day/night cycle of the first player's levels to midday, for screenshots.
    /// </summary>
    public static readonly CVarDef<bool> DevDaylight = CVarDef.Create("cmu.jeep.dev_daylight", false, CVar.SERVERONLY);
}
