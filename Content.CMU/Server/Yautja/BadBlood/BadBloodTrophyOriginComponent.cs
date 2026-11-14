namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// Records which body a trophy, or a severed part on its way to becoming one, was taken from.
/// The Lodge reads this for its per-body caps, so it follows the item through the cauldron and stack splits.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodTrophyOriginComponent : Component
{
    [DataField]
    public EntityUid Body;

    // the body had a player mind at some point this round. recorded only: the Lodge pays for mindless prey too
    [DataField]
    public bool WasPlayer;

    [DataField]
    public bool IsYautja;

    [DataField]
    public bool IsSynth;

    [DataField]
    public bool IsXeno;

    // larva or parasite
    [DataField]
    public bool IsLarva;

    // the body was dead when this was taken from it
    [DataField]
    public bool WasKill;
}
