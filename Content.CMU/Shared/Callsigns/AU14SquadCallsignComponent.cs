namespace Content.Shared._AU14.Callsigns;

// placed on a squad team entity to set the element word its members' callsigns use
// on the radio. squads default to color words (RED 6, YELLOW 1-2) instead of their
// phonetic names, so "ALPHA" in voice traffic can only ever mean another unit
[RegisterComponent]
public sealed partial class AU14SquadCallsignComponent : Component
{
    [DataField(required: true)]
    public string Word = string.Empty;
}
