using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ObjectiveMasterComponent : Component
{
    [DataField("completedObjectives")]
    public List<CompletedObjectiveRecord> CompletedObjectives = new();

    [Serializable]
    public sealed class CompletedObjectiveRecord
    {
        public EntityUid ObjectiveUid;
    }

    [NonSerialized] public HashSet<string> FinalObjectiveGivenFactions = new();

    [AutoNetworkedField] public bool IsActive;

    [DataField("Mode", required: true)]
    public string Mode = "ForceOnForce";

    [DataField("maxNeutralObjectives")]
    public int NeutralObjectives = 5;
    [DataField("minNeutralObjectives", required: false)]
    public int? MinNeutralObjectives = null;

    [DataField("currentwinpointsgovfor"), AutoNetworkedField]
    public int CurrentWinPointsGovfor = 0;
    [DataField("requiredwinpointsgovfor")]
    public int RequiredWinPointsGovfor = 100;
    [DataField("govforMinorObjectives", required: false)]
    public int GovforMinorObjectives = 10;
    [DataField("govforMajorObjectives", required: false)]
    public int GovforMajorObjectives = 8;
    [DataField("minGovforMinorObjectives", required: false)]
    public int? MinGovforMinorObjectives = null;
    [DataField("minGovforMajorObjectives", required: false)]
    public int? MinGovforMajorObjectives = null;

    [DataField("currentwinpointsopfor"), AutoNetworkedField]
    public int CurrentWinPointsOpfor = 0;
    [DataField("requiredwinpointsopfor")]
    public int RequiredWinPointsOpfor = 100;
    [DataField("opforMinorObjectives", required: false)]
    public int OpforMinorObjectives = 10;
    [DataField("opforMajorObjectives", required: false)]
    public int OpforMajorObjectives = 8;
    [DataField("minOpforMinorObjectives", required: false)]
    public int? MinOpforMinorObjectives = null;
    [DataField("minOpforMajorObjectives", required: false)]
    public int? MinOpforMajorObjectives = null;

    [DataField("currentwinpointsclf"), AutoNetworkedField]
    public int CurrentWinPointsClf = 0;
    [DataField("requiredwinpointsclf")]
    public int RequiredWinPointsClf = 100;
    [DataField("clfMinorObjectives", required: false)]
    public int CLFMinorObjectives = 10;
    [DataField("clfMajorObjectives", required: false)]
    public int CLFMajorObjectives = 8;
    [DataField("minCLFMinorObjectives", required: false)]
    public int? MinCLFMinorObjectives = null;
    [DataField("minCLFMajorObjectives", required: false)]
    public int? MinCLFMajorObjectives = null;

    [DataField("currentwinpointsscientist"), AutoNetworkedField]
    public int CurrentWinPointsScientist = 0;
    [DataField("requiredwinpointsscientist")]
    public int RequiredWinPointsScientist = 100;
    [DataField("scientistMinorObjectives", required: false)]
    public int ScientistMinorObjectives = 10;
    [DataField("scientistMajorObjectives", required: false)]
    public int ScientistMajorObjectives = 8;
    [DataField("minScientistMinorObjectives", required: false)]
    public int? MinScientistMinorObjectives = null;
    [DataField("minScientistMajorObjectives", required: false)]
    public int? MinScientistMajorObjectives = null;
}
