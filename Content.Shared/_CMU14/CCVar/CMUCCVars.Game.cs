using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> EnableEvacSfx =
        CVarDef.Create("cmu.game.enable_evac_sfx", false, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> MuteScriptedSounds =
        CVarDef.Create("cmu.game.mute_scripted_sfx", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
