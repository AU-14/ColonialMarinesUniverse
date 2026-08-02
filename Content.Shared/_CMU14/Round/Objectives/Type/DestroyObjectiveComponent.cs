namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class DestroyObjectiveComponent : Robust.Shared.GameObjects.Component
{
    [DataField]
    public bool UseAnyEntity { get; private set; }

    [DataField]
    public string SpawnMarkerId { get; private set; } = string.Empty;

    [DataField]
    public string TargetPrototype { get; private set; } = string.Empty;

    [DataField]
    public int SpawnCount { get; private set; } = 1;

    [DataField]
    public int DestroyCount { get; private set; } = 1;

    public Dictionary<string, int> AmountDestroyedPerFaction = new();
    public bool HasSpawned = false; // EntitiesSpawned
}
