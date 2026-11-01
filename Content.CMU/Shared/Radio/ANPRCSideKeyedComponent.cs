using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Radio;

/// <summary>
///     Issue stock that is not keyed to a side yet. Every platoon can fly either side and shares one
///     catalog and vendor set both ways, so vendors and requisitions stock this instead of a GOVFOR or
///     OPFOR item. It takes the side of the ship it appears on, failing that the first marine to pick it
///     up, and copies that side's variant onto itself in place so a vendor's auto-equip still holds it.
/// </summary>
[RegisterComponent]
public sealed partial class ANPRCSideKeyedComponent : Component
{
    [DataField(required: true)]
    public Dictionary<string, EntProtoId> Variants = new();

    // set the moment it is keyed; the component itself goes on a deferred removal
    public bool Keyed;
}
