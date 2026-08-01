using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Fetch;
[RegisterComponent]

public sealed partial class FetchObjectiveMarkerComponent: Component


{
    [DataField("generic", required: false)]
    public bool Generic { get; private set; } = false;

    [DataField("fetchid", required: false)]
    public string FetchId  { get; private set; } = "";

    public bool Used { get; set; } = false;

}
