using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives;

/// <summary>No scope set (legacy): determined via IsOnPlanet() / RMCPlanetComponent for selection</summary>
public enum MasterMapScope : byte
{
    Unset = 0,
    Planet = 1,
    Ship = 2,
    Station = 3,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ObjectiveMasterComponent : Component
{
    [NonSerialized] public HashSet<string> FinalObjectiveGivenFactions = new();
    [AutoNetworkedField] public bool IsActive;

    [DataField(required: true)]
    public string GamePreset = "ForceOnForce";

    /// <summary>Which maptype this ObjectiveMasterComponent is for, leaving it unset is obsolete - for legacy fallback</summary>
    [DataField]
    public MasterMapScope MapScope = MasterMapScope.Unset;

    [DataField]
    public int MaxNeutralObjectives = 5;
    [DataField]
    public int? MinNeutralObjectives;

    [DataField, AutoNetworkedField]
    public int CurrentWinPointsGovfor;
    [DataField]
    public int RequiredWinPointsGovfor = 100;
    [DataField]
    public int GovforMinorObjectives = 10;
    [DataField]
    public int GovforMajorObjectives = 5;
    [DataField]
    public int? MinGovforMinorObjectives;
    [DataField]
    public int? MinGovforMajorObjectives;

    [DataField, AutoNetworkedField]
    public int CurrentWinPointsOpfor;
    [DataField]
    public int RequiredWinPointsOpfor = 100;
    [DataField]
    public int OpforMinorObjectives = 10;
    [DataField]
    public int OpforMajorObjectives = 5;
    [DataField]
    public int? MinOpforMinorObjectives;
    [DataField]
    public int? MinOpforMajorObjectives;

    [DataField, AutoNetworkedField]
    public int CurrentWinPointsClf = 0;
    [DataField]
    public int RequiredWinPointsClf = 100;
    [DataField]
    public int CLFMinorObjectives = 10;
    [DataField]
    public int CLFMajorObjectives = 5;
    [DataField]
    public int? MinCLFMinorObjectives;
    [DataField]
    public int? MinCLFMajorObjectives;

    [DataField, AutoNetworkedField]
    public int CurrentWinPointsScientist;
    [DataField]
    public int RequiredWinPointsScientist = 100;
    [DataField]
    public int ScientistMinorObjectives = 10;
    [DataField]
    public int ScientistMajorObjectives = 5;
    [DataField]
    public int? MinScientistMinorObjectives;
    [DataField]
    public int? MinScientistMajorObjectives;
}
