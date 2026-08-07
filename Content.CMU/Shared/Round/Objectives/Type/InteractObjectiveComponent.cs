using Content.Shared._CMU14.Round.Objectives.Component;

namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class InteractObjectiveComponent : Robust.Shared.GameObjects.Component
{
    [DataField] public List<string> Interactables { get; private set; } = new();
    [DataField] public string DoAfterMessageBegin { get; private set; } = "You begin working...";
    [DataField] public string DoAfterMessageComplete { get; private set; } = "You finish working.";
    [DataField] public string SpawnMarkerId { get; private set; } = string.Empty;
    [DataField] public int SpawnCount { get; private set; } = 1;
    [DataField] public bool Spawn { get; private set; }
    [DataField] public int InteractionsNeeded { get; private set; } = 1;
    [DataField] public int CompletionsPerEnt { get; private set; } = 1;
    [DataField] public List<string> Skills { get; private set; } = new();
    [DataField] public List<string> Access { get; private set; } = new();
    [DataField] public List<string>? Tools { get; private set; }
    [DataField] public bool DestroyOnComplete { get; private set; }
    [DataField] public float InteractTime { get; private set; } = 4f;
    [DataField] public int TotalCompletionsNeeded { get; private set; }

    public bool HasSpawned;
    public Dictionary<string, int> CompletionsPerFaction { get; set; } = new();
}
