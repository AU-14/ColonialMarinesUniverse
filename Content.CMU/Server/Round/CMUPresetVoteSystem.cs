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

    [Dependency] private IPrototypeManager _prototypes = default!;

    private ProtoId<GamePresetPrototype>? _lastPlayedPreset;

    /// <summary>The only gamemode the vote offers while an admin has locked it, see <see cref="LockPreset"/>.</summary>
    public ProtoId<GamePresetPrototype>? LockedPreset { get; private set; }

    /// <summary>How many more rounds the lock lasts, counting the next one, or null if it lasts until cleared.</summary>
    public int? LockedRounds { get; private set; }

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

        if (LockedPreset == null || LockedRounds == null)
            return;

        LockedRounds--;
        if (LockedRounds <= 0)
            ClearLock();
    }

    /// <summary>
    ///     Makes the gamemode vote offer only <paramref name="preset"/> for the next <paramref name="rounds"/> rounds,
    ///     or until the lock is cleared if <paramref name="rounds"/> is null.
    /// </summary>
    public void LockPreset(ProtoId<GamePresetPrototype> preset, int? rounds = null)
    {
        LockedPreset = preset;
        LockedRounds = rounds is { } count ? Math.Max(1, count) : null;
    }

    public void ClearLock()
    {
        LockedPreset = null;
        LockedRounds = null;
    }

    /// <summary>
    ///     Narrows the gamemode vote's options: just the locked gamemode while there's a lock, otherwise everything but
    ///     the last mode played.
    /// </summary>
    public void FilterVotePresets(Dictionary<string, string> presets)
    {
        if (LockedPreset is { } locked && _prototypes.TryIndex(locked, out var preset))
        {
            presets.Clear();
            presets[preset.ID] = Loc.GetString(preset.ModeTitle);
            return;
        }

        RemoveLastPlayedPreset(presets);
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
