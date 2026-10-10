using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Construction;

/// <summary>
///     Siting rules for a freshly laid mast footing. RMC construction has no veto before the build, so the
///     footing is checked the moment it lands: a bad site takes the footing back down and returns the metal.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AU14MastSiteComponent : Component
{
    /// <summary>No other mast, mast stage or comms array may stand closer than this, in tiles.</summary>
    [DataField]
    public float MinSpacing = 20f;

    /// <summary>Tiles around the footing that must be free of walls for the guy lines.</summary>
    [DataField]
    public int ClearRadius = 1;

    /// <summary>What the footing cost to lay, returned in full when the site is refused.</summary>
    [DataField]
    public int Refund = 40;
}
