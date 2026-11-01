namespace Content.Shared.Preferences;

// CMU14: opt into the Foxtrot squad when joining GOVFOR/OPFOR.
public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    public bool FoxtrotSquadOptIn { get; private set; }

    public HumanoidCharacterProfile WithFoxtrotSquadOptIn(bool optIn)
    {
        return new(this) { FoxtrotSquadOptIn = optIn };
    }
}
