using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round;

public sealed partial class CMUPresetVoteSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private GameTicker _ticker = default!;

    private ProtoId<GamePresetPrototype>? _lastPlayedPreset;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound || ev.Old == GameRunLevel.InRound)
            return;

        // Ballots only select a future preset. Failed starts and lobby restarts must
        // retain the exclusion until another round actually starts, including fallbacks.
        _lastPlayedPreset = _ticker.CurrentPreset?.ID;
    }

    /// <summary>
    /// Excludes the last mode played when enabled, while retaining a sole eligible option.
    /// </summary>
    public void RemoveLastPlayedPreset(Dictionary<string, string> presets)
    {
        if (!_cfg.GetCVar(CCVars.VoteExcludeLastPlayed))
            return;

        if (_lastPlayedPreset is { } previous && presets.Count > 1)
            presets.Remove(previous);
    }
}
