using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Dropship.GunshipControls;

[RegisterComponent, NetworkedComponent]
public sealed partial class GunshipControlsComponent : Component
{
    /// <summary>
    /// Pilot firearm fire-control. The ship's direct-fire weapon only auto-selects
    /// for the pilot while this is true; toggled from the gunship console UI.
    /// </summary>
    [DataField]
    public bool Armed = true;
}
