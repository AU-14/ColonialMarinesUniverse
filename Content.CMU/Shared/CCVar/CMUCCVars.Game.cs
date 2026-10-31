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
        CVarDef.Create("cmu.fof.unidentified_marker_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether each round start rewrites the server name to "name | gamemode day Playtest". Off leaves game.hostname as set.
    /// </summary>
    public static readonly CVarDef<bool> CMUHostnameRoundSuffix =
        CVarDef.Create("cmu.game.hostname_round_suffix", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> EnableEvacSfx =
        CVarDef.Create("cmu.game.enable_evac_sfx", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Warship fixtures tint, dim or darken with the alert level and hull floodlights go dark on red and black.
    /// Flipping it restores every managed fixture to its prototype state, turning it on applies each ship's current level.
    /// </summary>
    public static readonly CVarDef<bool> EnableWarshipAlertLights =
        CVarDef.Create("cmu.game.enable_warship_alert_lights", false, CVar.SERVERONLY | CVar.ARCHIVE);

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

    /// <summary>
    /// Seconds a loose, empty, detachable magazine lies on the ground before the loose sweep
    /// despawns it. Long fuse on purpose: rounds run 2h+ and spent mags are battlefield texture;
    /// the sweep only fights entity bloat late-round. Zero disables. Loaded magazines are never touched.
    /// </summary>
    public static readonly CVarDef<float> LooseMagazineDespawnTime =
        CVarDef.Create("cmu.game.loose_magazine_despawn_time", 1800f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Seconds a loose trash-tagged entity lies on the ground before the loose sweep despawns
    /// it. Zero disables the trash sweep. Papers are excluded, matching nuke:trash.
    /// </summary>
    public static readonly CVarDef<float> TrashDespawnTime =
        CVarDef.Create("cmu.game.trash_despawn_time", 3600f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> HoldRoundEnd =
        CVarDef.Create("cmu.game.hold_round_end", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> CritWhisper =
        CVarDef.Create("cmu.game.crit_whisper", true, CVar.SERVERONLY);

    [CVarControl(AdminFlags.VarEdit, min: 0, max: 255)]
    public static readonly CVarDef<int> FoFMaxGap =
        CVarDef.Create("cmu.game.fof_max_force_balance_gap", 3, CVar.SERVERONLY);

    /// <summary>
    /// MREs aboard the warship stay sealed while the mess hall is serving: unconditionally for the first ten
    /// minutes of the round, then only while the mess has food to serve. A hijacked dropship still overrides it.
    /// </summary>
    [CVarControl(AdminFlags.VarEdit)]
    public static readonly CVarDef<bool> ShipMreLock =
        CVarDef.Create("cmu.game.ship_mre_lock", true, CVar.ARCHIVE | CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// The preset an admin locked the gamemode vote to, empty when unlocked. Archived so the lock survives
    /// server restarts and round recycling.
    /// </summary>
    public static readonly CVarDef<string> GamemodeLockPreset =
        CVarDef.Create("cmu.game.gamemode_lock_preset", "", CVar.SERVER | CVar.ARCHIVE);

    /// <summary>How many more rounds the gamemode vote lock covers, counting the next one. Zero means until cleared.</summary>
    public static readonly CVarDef<int> GamemodeLockRounds =
        CVarDef.Create("cmu.game.gamemode_lock_rounds", 0, CVar.SERVER | CVar.ARCHIVE);
}
