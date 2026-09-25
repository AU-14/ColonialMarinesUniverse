using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>Show the cosmetic Party Time lineup. Can be toggled live in the pre-round lobby.</summary>
    public static readonly CVarDef<bool> LobbyPartyTime =
        CVarDef.Create("cmu.lobby_party_time", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Enable the lobby lineup and its automatic/requested air show independently.</summary>
    public static readonly CVarDef<bool> LobbyPartyTimeFlyby =
        CVarDef.Create("cmu.lobby_party_time_flyby", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Enable the lobby lineup and its automatic/requested parade independently.</summary>
    public static readonly CVarDef<bool> LobbyPartyTimeParade =
        CVarDef.Create("cmu.lobby_party_time_parade", false, CVar.SERVER | CVar.REPLICATED);
}
