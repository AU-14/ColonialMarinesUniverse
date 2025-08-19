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


    [DataField("clfMinorObjectives", required: true)]
    public int CLFMinorObjectives = 10;

    [DataField("clfMajorObjectives", required: false)]
    public int CLFMajorObjectives = 8;

    [DataField("winpointsopfor")]
    public int Winpointsopfor = 100;

    [DataField("winpointsgovfor")]
    public int Winpointsgovfor = 100;


    [DataField("winpointsclf")]
    public int Winpointsclf = 100;

    [DataField("scientistMinorObjectives", required: false)]
    public int ScientistMinorObjectives = 10;

    [DataField("scientistMajorObjectives", required: false)]
    public int ScientistMajorObjectives = 8;

    [DataField("winpointsscientist")]
    public int Winpointsscientist = 100;

}
