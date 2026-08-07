using Content.Shared._CMU14.Round.Objectives.Component;

namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class InteractTrackerComponent : Robust.Shared.GameObjects.Component
{
    public EntityUid ObjectiveUid;
    public Dictionary<string, int> CompletionsPerFaction { get; set; } = new();
    public Dictionary<string, int> InteractionsPerFaction { get; set; } = new();
}
