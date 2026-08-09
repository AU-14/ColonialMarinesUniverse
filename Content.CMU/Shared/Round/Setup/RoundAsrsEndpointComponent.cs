namespace Content.Shared.CMU.Round;

/// <summary>
/// Configures ASRS-specific behavior for a semantic round setup endpoint.
/// </summary>
[RegisterComponent]
public sealed partial class RoundAsrsEndpointComponent : Component
{
    /// <summary>
    /// Keeps an access configuration authored directly on the mapped console.
    /// </summary>
    [DataField]
    public bool PreserveMappedAccess { get; private set; }
}
