namespace Content.Server.Humanoid.Components;

[RegisterComponent]
public sealed partial class RandomHumanoidAppearanceComponent : Component
{
    [DataField]
    public bool RandomizeName = true;

    /// <summary>
    /// Optional hairstyle applied after randomization.
    /// </summary>
    [DataField]
    public string? Hair;
}
