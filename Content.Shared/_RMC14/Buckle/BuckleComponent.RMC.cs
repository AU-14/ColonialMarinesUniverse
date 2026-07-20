using Robust.Shared.GameStates;

namespace Content.Shared.Buckle.Components;

public sealed partial class BuckleComponent
{
    /// <summary>
    /// Optional override for the strap's buckle DoAfter duration.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float? BuckleDelay;

    /// <summary>
    /// Whether an empty-hand click on this entity may unbuckle it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ClickUnbuckle = true;
}
