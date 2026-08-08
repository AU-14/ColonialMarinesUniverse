using Content.Server.AU14.Scenario;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Shared.Profiling;

namespace Content.Server.AU14.Round;

/// <summary>
/// Coordinates the one-way transition from mutable lobby choices to the live round world.
/// </summary>
public sealed partial class CMURoundDirectorSystem : EntitySystem
{
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private AuRoundSystem _round = default!;

    private readonly CMURoundDirectorState _state = new();

    /// <summary>
    /// Monotonically increasing identifier used to reject stale round-preparation work.
    /// </summary>
    [ViewVariables]
    public int Generation => _state.Generation;

    /// <summary>
    /// Highest completed preparation phase in the current generation.
    /// </summary>
    [ViewVariables]
    public CMURoundPhase Phase => _state.Phase;

    /// <summary>
    /// The immutable lobby selection consumed by map loading and live scenario planning.
    /// </summary>
    public RoundPlanSelectionSnapshot? Selection => _state.Selection;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    /// <summary>
    /// Freezes the first selection observed at the existing map-preload boundary.
    /// Later calls in the same generation return the same context.
    /// </summary>
    public RoundPlanSelectionSnapshot FreezeSelection(int playerCount, string? fallbackPresetId)
    {
        using var profile = _prof.Group("CMU Round Selection Freeze");

        if (_state.Selection is { } existing)
            return existing;

        _round.FinalizeVoteSequence(playerCount, fallbackPresetId);

        var presetId = _round.SelectedPreset?.ID ?? fallbackPresetId ?? string.Empty;
        var candidate = _round.CaptureRoundPlanSelection(
            playerCount,
            presetId,
            _round.SelectedThreat?.ID);

        if (!_state.TryFreezeSelection(candidate, out var frozen))
            return frozen;

        _round.FreezeRoundPlanSelection(frozen);
        RaisePhaseChanged();
        return frozen;
    }

    internal void MarkMapsLoaded()
    {
        if (_state.TryMarkMapsLoaded())
            RaisePhaseChanged();
    }

    internal void MarkWorldInitialized()
    {
        if (_state.TryMarkWorldInitialized())
            RaisePhaseChanged();
    }

    internal void MarkPlayersSpawned()
    {
        if (_state.TryMarkPlayersSpawned())
            RaisePhaseChanged();
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.InRound && _state.TryEnterRound())
            RaisePhaseChanged();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _round.ResetRoundPlanSelection();
        _state.Reset();
        RaisePhaseChanged();
    }

    private void RaisePhaseChanged()
    {
        var changed = new CMURoundPhaseChangedEvent(
            _state.Generation,
            _state.Phase,
            _state.Prerequisites);
        RaiseLocalEvent(ref changed);
    }
}

/// <summary>
/// Announces a one-way phase transition for one round-preparation generation.
/// </summary>
[ByRefEvent]
public readonly record struct CMURoundPhaseChangedEvent(
    int Generation,
    CMURoundPhase Phase,
    CMURoundPrerequisite Prerequisites);
