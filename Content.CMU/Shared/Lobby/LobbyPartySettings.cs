using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.Lobby;

/// <summary>The base lineup, air show, and parade routines can each be enabled independently.</summary>
public static class LobbyPartySettings
{
    public static bool IsEnabled(IConfigurationManager config) =>
        config.GetCVar(CCVars.LobbyPartyTime) ||
        config.GetCVar(CCVars.LobbyPartyTimeFlyby) || config.GetCVar(CCVars.LobbyPartyTimeParade);

    public static bool IsShowEnabled(IConfigurationManager config, LobbyPartyShow show) => show switch
    {
        LobbyPartyShow.Flyby => config.GetCVar(CCVars.LobbyPartyTimeFlyby),
        LobbyPartyShow.Parade or LobbyPartyShow.SupplyScramble => config.GetCVar(CCVars.LobbyPartyTimeParade),
        _ => false,
    };

    public static bool TryNextShow(IConfigurationManager config, LobbyPartyShow preferred, out LobbyPartyShow show)
    {
        show = preferred;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (IsShowEnabled(config, show))
                return true;
            show = Next(show);
        }
        return false;
    }

    public static LobbyPartyShow Next(LobbyPartyShow show) => show switch
    {
        LobbyPartyShow.Flyby => LobbyPartyShow.Parade,
        LobbyPartyShow.Parade => LobbyPartyShow.SupplyScramble,
        _ => LobbyPartyShow.Flyby,
    };
}
