using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives;
[RegisterComponent, NetworkedComponent]

public sealed partial class FetchObjectiveReturnPointComponent: Component


{
    [DataField("generic", required: false)]
    public bool Generic { get; private set; } = false;

    [DataField("returnid", required: false)]
    public string FetchId  { get; private set; } = "";
    // where to return the fetched item, if none is set will be generic

}
