namespace Content.Server._CMU14.Humanoid;

/// <summary>
/// Marks a humanoid as currently having their hair tied back, storing the original hairstyle
/// so it can be restored by the "Untie Hair" verb.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUTieHairSystem))]
public sealed partial class CMUTiedHairComponent : Component
{
    [DataField]
    public string OriginalHairId = string.Empty;

    [DataField]
    public List<Color> OriginalHairColors = new();
}
