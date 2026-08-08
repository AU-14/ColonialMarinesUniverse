using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private CMURoundDirectorSystem _cmuRoundDirector = default!;

    private RoundPlanSelectionSnapshot FreezeCmuRoundSelection()
    {
        return _cmuRoundDirector.FreezeSelection(
            _playerManager.PlayerCount,
            CurrentPreset?.ID ?? Preset?.ID);
    }
}
