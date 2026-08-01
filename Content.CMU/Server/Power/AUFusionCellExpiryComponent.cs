namespace Content.Server.AU14.Power;

[RegisterComponent]
public sealed partial class AUFusionCellExpiryComponent : Component
{
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public TimeSpan? InsertedAt;
}
