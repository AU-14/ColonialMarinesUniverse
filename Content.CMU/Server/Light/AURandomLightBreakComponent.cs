namespace Content.Server.AU14.Light;

[RegisterComponent]
public sealed partial class AURandomLightBreakComponent : Component
{
    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    [DataField]
    public float BreakChance = 0.0033f;

    public TimeSpan NextCheckAt;
}
