using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Fetch;
[RegisterComponent, NetworkedComponent]

public sealed partial class FetchObjectiveComponent: Component
{

    [DataField("entitytospawn", required: true)]
    public string EntityToSpawn { get; private set; } = default!;


    [DataField("markerentity", required: false)]
    public string MarkerEntity { get; private set; } = default!;
    //if none uses generic, used for spawning


    [DataField("amountospawn", required: false)]
    public int AmountToSpawn { get; private set; } = 1;

    [DataField("amounttofetch", required: false)]
    public int AmountToFetch { get; private set; } = 1;
    //amount needed to complete the objective
    public int AmountFetched = 0;

    public FetchObjectiveReturnPointComponent ReturnPoint;

    [DataField("customereturnpointid", required: false)]
    public string CustomReturnPointId { get; private set; } = "";
    // where a fetched item should be brought, based on fetchid, if none is set will be generic
}
