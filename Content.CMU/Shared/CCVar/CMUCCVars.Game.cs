using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> EnableEvacSfx =
        CVarDef.Create("cmu.game.enable_evac_sfx", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VoteStartDelay =
        CVarDef.Create("cmu.game.vote_start_delay", 60f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> VoteStartDelayMinPlayers =
        CVarDef.Create("cmu.game.vote_start_delay_min_players", 10, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> MuteScriptedSounds =
        CVarDef.Create("cmu.game.mute_scripted_sfx", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> SpentCasingDespawnTime =
        CVarDef.Create("cmu.game.spent_casing_despawn_time", 300f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> HoldRoundEnd =
        CVarDef.Create("cmu.game.hold_round_end", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> CritWhisper =
        CVarDef.Create("cmu.game.crit_whisper", true, CVar.SERVERONLY);
}
