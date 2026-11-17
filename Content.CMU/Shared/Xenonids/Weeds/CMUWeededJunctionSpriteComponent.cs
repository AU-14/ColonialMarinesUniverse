namespace Content.Shared.CMU14.Xenonids.Weeds;

// weed overlay that copies the junction of the window or frame it grows on, cmss13 weedwall/window and weedwall/frame style.
// the client picks "<base><junction>"; junctions with no sprite fall back to the nearest straight run
[RegisterComponent]
public sealed partial class CMUWeededJunctionSpriteComponent : Component
{
    [DataField(required: true)]
    public string Base = string.Empty;

    // junctions the rsi has a state for, cmss13 only drew straight runs
    [DataField]
    public List<int> States = new() { 0, 1, 2, 3, 4, 8, 12 };
}
