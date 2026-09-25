using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.Lobby;

/// <summary>The base lineup and its two optional shows can each be enabled independently.</summary>
public static class LobbyPartySettings
{
    public static bool IsEnabled(IConfigurationManager config) =>
        config.GetCVar(CCVars.LobbyPartyTime) ||
        config.GetCVar(CCVars.LobbyPartyTimeFlyby) || config.GetCVar(CCVars.LobbyPartyTimeParade);

    public static bool IsShowEnabled(IConfigurationManager config, LobbyPartyShow show) => show switch
    {
        LobbyPartyShow.Flyby => config.GetCVar(CCVars.LobbyPartyTimeFlyby),
        LobbyPartyShow.Parade => config.GetCVar(CCVars.LobbyPartyTimeParade),
        _ => false,
    };

    public static bool TryNextShow(IConfigurationManager config, LobbyPartyShow preferred, out LobbyPartyShow show)
    {
        show = preferred;
        if (IsShowEnabled(config, show))
            return true;
        show = preferred == LobbyPartyShow.Flyby ? LobbyPartyShow.Parade : LobbyPartyShow.Flyby;
        return IsShowEnabled(config, show);
    }
}
