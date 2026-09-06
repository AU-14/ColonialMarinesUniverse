namespace Content.Shared._CMU14.Round.Antags.StrikeOrganizer;

/// <summary>
/// A strike petition. Used in hand to sign it; CMB alerts escalate with the signature count.
/// </summary>
[RegisterComponent]
public sealed partial class StrikePetitionComponent : Component
{
    /// <summary>
    /// Signatures needed for the strike vote to pass.
    /// </summary>
    [DataField]
    public int Goal = 10;

    /// <summary>
    /// Names of the colonists who signed, in order.
    /// </summary>
    [DataField]
    public List<string> Signatures = new();

    public bool FaxedHalf;
    public bool FaxedFull;
}
