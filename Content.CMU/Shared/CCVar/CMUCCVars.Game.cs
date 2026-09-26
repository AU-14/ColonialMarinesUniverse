using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Replace marine HUD identifiers with a question mark for other factions without a uniform recognized by the viewer.
    /// </summary>
    public static readonly CVarDef<bool> ForceOnForceUnidentifiedMarkerEnabled =
        CVarDef.Create("cmu.fof.unidentified_marker_enabled", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> EnableEvacSfx =
        CVarDef.Create("cmu.game.enable_evac_sfx", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VoteStartDelay =
        CVarDef.Create("cmu.game.vote_start_delay", 60f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> VoteStartDelayMinPlayers =
        CVarDef.Create("cmu.game.vote_start_delay_min_players", 10, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Excludes the last played gamemode from new preset votes, unless it is the sole eligible option.
    /// </summary>
    public static readonly CVarDef<bool> VoteExcludeLastPlayed =
        CVarDef.Create("cmu.game.vote_exclude_last_played", true, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> MuteScriptedSounds =
        CVarDef.Create("cmu.game.mute_scripted_sfx", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> SpentCasingDespawnTime =
        CVarDef.Create("cmu.game.spent_casing_despawn_time", 300f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> HoldRoundEnd =
        CVarDef.Create("cmu.game.hold_round_end", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> CritWhisper =
        CVarDef.Create("cmu.game.crit_whisper", true, CVar.SERVERONLY);

    [CVarControl(AdminFlags.VarEdit, min: 0, max: 255)]
    public static readonly CVarDef<int> FoFMaxGap =
        CVarDef.Create("cmu.game.fof_max_force_balance_gap", 3, CVar.SERVERONLY);
}
