using Content.Shared.Paper;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Intel;

/// <summary>
/// When this objective item is recovered at an Analyzer Machine, the Analyzer prints a readout listing the
/// areas of the map with the most intel still out there.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUIntelSurveyOnAnalyzeComponent : Component
{
    /// <summary>How many areas the readout lists.</summary>
    [DataField]
    public int Areas = 3;

    [DataField]
    public EntProtoId<PaperComponent> Paper = "CMPaper";

    [DataField]
    public string Title = "CIR-60 Recovered Data: Intel Survey";
}
