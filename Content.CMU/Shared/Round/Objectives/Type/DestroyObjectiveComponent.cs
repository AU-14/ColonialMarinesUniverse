namespace Content.Shared.CMU14.Round.Objectives.Type;

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

    /// <summary>
    /// When set, a target only counts once the Destructible pipeline destroys it (DestructionEventArgs).
    /// A plain deletion, such as deconstruction or cleanup, pays nothing. Off by default: any deletion counts.
    /// </summary>
    [DataField]
    public bool RequireDestruction { get; private set; }

    public Dictionary<string, int> AmountDestroyedPerFaction = new();
    public bool HasSpawned = false; // EntitiesSpawned
}
