namespace Content.Shared._CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class FetchReturnPointComponent : Robust.Shared.GameObjects.Component
{
    [DataField]
    public bool Generic { get; private set; }

    [DataField]
    public string FetchId { get; private set; } = string.Empty;

    [DataField]
    public string ReturnPointFaction { get; private set; } = string.Empty;
}
