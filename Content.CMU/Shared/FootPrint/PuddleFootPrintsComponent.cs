namespace Content.Shared.FootPrint;

[RegisterComponent]
public sealed partial class PuddleFootPrintsComponent : Component
{
    [DataField]
    public float SizeRatio = 0.2f;

    [DataField]
    public float OffPercent = 80f;

    [ViewVariables]
    public HashSet<EntityUid> ActivatedEntities = new();
}
