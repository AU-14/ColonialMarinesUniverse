using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives;
[RegisterComponent, NetworkedComponent]

public sealed partial class ObjectiveMasterComponent : Component
{

    [DataField("Mode", required: true)]
    public string Mode = "ForceOnForce";


    [DataField("govforMinorObjectives", required: false)]
    public int GovforMinorObjectives = 10;

    [DataField("govforMajorObjectives", required: false)]
    public int GovforMajorObjectives = 8;


    [DataField("opforMinorObjectives", required: false)]
    public int OpforMinorObjectives = 10;

    [DataField("opforMajorObjectives", required: false)]
    public int OpforMajorObjectives = 8;


    [DataField("clfMinorObjectives", required: false)]
    public int CLFMinorObjectives = 10;

    [DataField("clfMajorObjectives", required: false)]
    public int CLFMajorObjectives = 8;


    [DataField("scientistMinorObjectives", required: false)]
    public int ScientistMinorObjectives = 10;

    [DataField("scientistMajorObjectives", required: false)]
    public int ScientistMajorObjectives = 8;


    [DataField("currentwinpointsgovfor")]
    public int CurrentWinPointsGovfor = 0;
    [DataField("currentwinpointsopfor")]
    public int CurrentWinPointsOpfor = 0;
    [DataField("currentwinpointsclf")]
    public int CurrentWinPointsClf = 0;
    [DataField("currentwinpointsscientist")]
    public int CurrentWinPointsScientist = 0;

    [DataField("requiredwinpointsgovfor")]
    public int RequiredWinPointsGovfor = 100;
    [DataField("requiredwinpointsopfor")]
    public int RequiredWinPointsOpfor = 100;
    [DataField("requiredwinpointsclf")]
    public int RequiredWinPointsClf = 100;
    [DataField("requiredwinpointsscientist")]
    public int RequiredWinPointsScientist = 100;

    // --- NEW: Track completed objectives ---
    [DataField("completedObjectives")]
    public List<CompletedObjectiveRecord> CompletedObjectives = new();

    [Serializable]
    public class CompletedObjectiveRecord
    {
        public EntityUid ObjectiveUid;
    }

    [NonSerialized]
    public HashSet<string> FinalObjectiveGivenFactions = new();

}
