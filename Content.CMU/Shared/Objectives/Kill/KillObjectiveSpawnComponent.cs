namespace Content.Shared.AU14.Objectives.Kill;

[DataDefinition]
public sealed partial class KillObjectiveSpawnComponent
{
    [DataField("generic", required: false)]
    public bool Generic { get; private set; } = false;

    [DataField("FetchId", required: false)]
    public string FetchId  { get; private set; } = "";
}
