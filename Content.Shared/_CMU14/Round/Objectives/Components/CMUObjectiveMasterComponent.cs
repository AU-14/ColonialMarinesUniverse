using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Round.Objectives.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUObjectiveMasterComponent : Robust.Shared.GameObjects.Component
{
    [DataDefinition]
    public sealed partial class FactionObjectiveData
    {
        [DataField] public int RequiredWinPoints = 100;

        [DataField] public int MaxMinorObjectives = 10;
        [DataField] public int? MinMinorObjectives;

        [DataField] public int MaxMajorObjectives = 5;
        [DataField] public int? MinMajorObjectives;

        public int CurrentWinPoints;
    }

    [DataField(required: true)]
    public string GamePreset { get; set; } = "DistressSignal";

    [DataField]
    public int MaxNeutralObjectives { get; set; } = 5;

    [DataField]
    public int? MinNeutralObjectives { get; set; }

    [DataField]
    public Dictionary<string, FactionObjectiveData> Factions { get; set; } = new()
    {
        ["govfor"] = new(),
        ["opfor"] = new(),
        ["clf"] = new(),
        ["scientist"] = new(),
    };

    public HashSet<string> FactionsGivenFinalObjective { get; set; } = new();

    [AutoNetworkedField] public bool IsActive;
    public bool SelectionComplete;

    public FactionObjectiveData GetOrCreateFactionData(string faction)
    {
        if (!Factions.TryGetValue(faction, out var data))
            Factions[faction] = data = new FactionObjectiveData();
        return data;
    }
}
