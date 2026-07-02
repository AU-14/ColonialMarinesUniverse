using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ObjectiveMasterComponent : Component
{
    [NonSerialized] public HashSet<string> FinalObjectiveGivenFactions = new();
    [AutoNetworkedField] public bool IsActive;

    [DataField(required: true)]
    public string GamePreset = "ForceOnForce";

    [DataField]
    public int MaxNeutralObjectives = 5;
    [DataField]
    public int? MinNeutralObjectives;

    [DataField] // no clientside code requires networking
    public Dictionary<string, FactionObjectiveData> Factions { get; set; } = new()
    {
        ["govfor"] = new(),
        ["opfor"] = new(),
        ["clf"] = new(),
        ["scientist"] = new(),
    };

    [DataDefinition]
    public sealed partial class FactionObjectiveData
    {
        [DataField] public int CurrentWinPoints;
        [DataField] public int RequiredWinPoints = 100;
        [DataField] public int MinorObjectives = 10;
        [DataField] public int? MinMinorObjectives;
        [DataField] public int MajorObjectives = 5;
        [DataField] public int? MinMajorObjectives;
    }

    public FactionObjectiveData GetOrCreateFactionData(string faction)
    {
        if (!Factions.TryGetValue(faction, out var data))
            Factions[faction] = data = new FactionObjectiveData();
        return data;
    }
}
