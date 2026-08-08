using Content.Server.AU14.Scenario;

namespace Content.Server.AU14.Round;

/// <summary>
/// Completed prerequisites for the current round preparation generation.
/// </summary>
[Flags]
public enum CMURoundPrerequisite : byte
{
    None = 0,
    SelectionFrozen = 1 << 0,
    MapsLoaded = 1 << 1,
    WorldInitialized = 1 << 2,
    PlayersSpawned = 1 << 3,
}

/// <summary>
/// Highest completed phase in the current round preparation generation.
/// </summary>
public enum CMURoundPhase : byte
{
    AwaitingSelection,
    SelectionFrozen,
    MapsLoaded,
    WorldInitialized,
    PlayersSpawned,
    InRound,
}

/// <summary>
/// Generation-scoped state for the one-way round preparation pipeline.
/// </summary>
internal sealed class CMURoundDirectorState
{
    public int Generation { get; private set; } = 1;
    public CMURoundPhase Phase { get; private set; } = CMURoundPhase.AwaitingSelection;
    public CMURoundPrerequisite Prerequisites { get; private set; }
    public RoundPlanSelectionSnapshot? Selection { get; private set; }

    public bool TryFreezeSelection(
        RoundPlanSelectionSnapshot selection,
        out RoundPlanSelectionSnapshot frozen)
    {
        if (Selection is { } existing)
        {
            frozen = existing;
            return false;
        }

        Selection = selection;
        Prerequisites |= CMURoundPrerequisite.SelectionFrozen;
        Phase = CMURoundPhase.SelectionFrozen;
        frozen = selection;
        return true;
    }

    public bool TryMarkMapsLoaded()
    {
        return TryAdvance(
            CMURoundPrerequisite.SelectionFrozen,
            CMURoundPrerequisite.MapsLoaded,
            CMURoundPhase.MapsLoaded);
    }

    public bool TryMarkWorldInitialized()
    {
        return TryAdvance(
            CMURoundPrerequisite.MapsLoaded,
            CMURoundPrerequisite.WorldInitialized,
            CMURoundPhase.WorldInitialized);
    }

    public bool TryMarkPlayersSpawned()
    {
        return TryAdvance(
            CMURoundPrerequisite.WorldInitialized,
            CMURoundPrerequisite.PlayersSpawned,
            CMURoundPhase.PlayersSpawned);
    }

    public bool TryEnterRound()
    {
        if (!Prerequisites.HasFlag(CMURoundPrerequisite.PlayersSpawned) ||
            Phase == CMURoundPhase.InRound)
        {
            return false;
        }

        Phase = CMURoundPhase.InRound;
        return true;
    }

    public void Reset()
    {
        Generation++;
        Phase = CMURoundPhase.AwaitingSelection;
        Prerequisites = CMURoundPrerequisite.None;
        Selection = null;
    }

    private bool TryAdvance(
        CMURoundPrerequisite required,
        CMURoundPrerequisite completed,
        CMURoundPhase phase)
    {
        if (!Prerequisites.HasFlag(required) || Prerequisites.HasFlag(completed))
            return false;

        Prerequisites |= completed;
        Phase = phase;
        return true;
    }
}
