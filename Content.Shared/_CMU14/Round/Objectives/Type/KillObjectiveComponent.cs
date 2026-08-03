namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class KillObjectiveComponent : Robust.Shared.GameObjects.Component
{
    [DataField] public string? TargetPrototype;
    [DataField] public string? SpecificJob;
    [DataField] public bool SynthOnly;
    [DataField] public bool SpawnMob;

    [DataField] public bool Catalog = true;
    [DataField] public string SpawnMarkerId = string.Empty;
    [DataField] public int SpawnCount = 1;
    [DataField] public string FactionToKill = string.Empty;
    [DataField] public int KillCount = 1;
    [DataField] public bool RespawnOnRepeat;
    [DataField] public bool CountArrest = true;

    public bool HasSpawned;
    public Dictionary<string, int> AmountKilledPerFaction = new();
}
