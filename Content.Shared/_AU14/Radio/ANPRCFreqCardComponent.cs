namespace Content.Shared._AU14.Radio;

// signal operating instructions card, its paper content is generated from the
// round's frequency plan at spawn
[RegisterComponent]
public sealed partial class ANPRCFreqCardComponent : Component
{
    [DataField]
    public string Faction = "govfor";
}
