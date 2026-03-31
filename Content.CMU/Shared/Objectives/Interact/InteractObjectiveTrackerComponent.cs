using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Interact;

/// <summary>
/// Placed on entities that are targets for an Interact objective.
/// Links them back to the objective entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InteractObjectiveTrackerComponent : Component
{
    /// <summary>
    /// The objective entity this interactable belongs to.
    /// </summary>
    public EntityUid ObjectiveUid;

    /// <summary>
    /// How many times this specific entity has been interacted with so far.
    /// Resets per-completion cycle if the objective repeats.
    /// </summary>
    public int CurrentInteractions = 0;

    /// <summary>
    /// How many times this entity has been fully completed (reached InteractionsNeeded).
    /// Tracked per-faction for faction-neutral objectives.
    /// </summary>
    public Dictionary<string, int> CompletionsPerFaction { get; set; } = new();

    /// <summary>
    /// Per-faction interaction counts (for faction-neutral objectives).
    /// </summary>
    public Dictionary<string, int> InteractionsPerFaction { get; set; } = new();
}

