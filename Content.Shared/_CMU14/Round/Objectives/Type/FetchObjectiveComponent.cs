namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class FetchObjectiveComponent : Robust.Shared.GameObjects.Component
{
    [DataField] public bool UseAnyEntity;
    [DataField] public string SpawnMarkerId = string.Empty;
    [DataField] public string TargetPrototype = string.Empty;
    [DataField] public int SpawnCount = 1;
    [DataField] public int FetchCount = 1;
    [DataField] public string? SpawnOther;
    [DataField] public string? CustomReturnPointId;
    [DataField] public bool RespawnOnRepeat;
    [DataField] public bool Catalog = true;

    public bool HasSpawned;
    public bool LateActivation;
    public Dictionary<string, int> AmountFetchedPerFaction = new();
}
