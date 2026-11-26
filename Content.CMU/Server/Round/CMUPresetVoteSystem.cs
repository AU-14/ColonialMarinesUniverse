using System.Linq;
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

    /// <summary>The only gamemodes the vote offers while an admin has locked them, see <see cref="LockPresets"/>.</summary>
    public IReadOnlyList<ProtoId<GamePresetPrototype>> LockedPresets =>
        _cfg.GetCVar(CCVars.GamemodeLockPreset)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(preset => (ProtoId<GamePresetPrototype>) preset)
            .ToList();

    /// <summary>How many more rounds the lock lasts, counting the next one, or null if it lasts until cleared.</summary>
    public int? LockedRounds =>
        _cfg.GetCVar(CCVars.GamemodeLockRounds) is { } rounds && rounds > 0 ? rounds : null;

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

        if (LockedPresets.Count == 0 || LockedRounds is not { } locked)
            return;

        if (locked <= 1)
            ClearLock();
        else
            _cfg.SetCVar(CCVars.GamemodeLockRounds, locked - 1);
    }

    /// <summary>
    ///     Makes the gamemode vote offer only <paramref name="presets"/> for the next <paramref name="rounds"/> rounds,
    ///     or until the lock is cleared if <paramref name="rounds"/> is null.
    /// </summary>
    public void LockPresets(IEnumerable<ProtoId<GamePresetPrototype>> presets, int? rounds = null)
    {
        _cfg.SetCVar(CCVars.GamemodeLockPreset, string.Join(",", presets.Distinct().Select(preset => preset.Id)));
        _cfg.SetCVar(CCVars.GamemodeLockRounds, rounds is { } count ? Math.Max(1, count) : 0);
    }

    public void ClearLock()
    {
        _cfg.SetCVar(CCVars.GamemodeLockPreset, "");
        _cfg.SetCVar(CCVars.GamemodeLockRounds, 0);
    }

    /// <summary>
    ///     Narrows the gamemode vote's options: just the locked gamemodes while there's a lock, otherwise everything but
    ///     the last mode played.
    /// </summary>
    public void FilterVotePresets(Dictionary<string, string> presets)
    {
        var offered = new List<GamePresetPrototype>();
        foreach (var locked in LockedPresets)
            if (_prototypes.TryIndex(locked, out var preset))
                offered.Add(preset);

        // A lock whose presets no longer resolve at all falls back to the unlocked behavior.
        if (offered.Count > 0)
        {
            presets.Clear();
            foreach (var preset in offered)
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
