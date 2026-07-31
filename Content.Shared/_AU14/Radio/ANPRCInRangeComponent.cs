namespace Content.Shared._AU14.Radio;

// transient marker stamped on a radio for the duration of one send/receive,
// carrying how good its link to the nearest covering anchor is
[RegisterComponent]
public sealed partial class ANPRCInRangeComponent : Component
{
    // 1 = on top of the anchor, 0.5 = full-range boundary, 0 = partial fringe
    [DataField]
    public float Quality = 1f;
}
