namespace Content.Shared.CMU14.Round.Objectives.Type;

[RegisterComponent]
public sealed partial class FetchItemComponent : Robust.Shared.GameObjects.Component
{
    public EntityUid ObjectiveUid;
    public bool Fetched;
}
