namespace Content.Shared.CMU14.Maps;

/// <summary>
/// Invisible entity a charge is planted on when it's placed on a destroyable tile.
/// Deleting it while a charge is stuck to it breaks the tile.
/// </summary>
[RegisterComponent]
public sealed partial class CMUTileChargeAnchorComponent : Component
{
    /// <summary>Set once a charge has been stuck to this anchor.</summary>
    [ViewVariables]
    public bool Armed;

    /// <summary>How long an anchor waits for a charge to be planted before cleaning itself up.</summary>
    [DataField]
    public TimeSpan UnarmedLifetime = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan ExpireAt;
}
