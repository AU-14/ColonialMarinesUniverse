namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class ArrestObjectiveComponent : Robust.Shared.GameObjects.Component
{
    [DataField] public string FactionToArrest = string.Empty;
    [DataField] public string? SpecificJob;
    [DataField] public bool SynthOnly;
    [DataField] public string? TargetPrototype;
    [DataField] public bool SpawnMob;
    [DataField] public string SpawnMarkerId = string.Empty;
    [DataField] public int SpawnCount = 1;
    [DataField] public int ArrestCount = 1;
    [DataField] public bool RespawnOnRepeat;
    [DataField] public bool RemoveKillMark = true;

    public bool HasSpawned;
    public Dictionary<string, int> AmountArrestedPerFaction = new();
}
