namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// Records which body a cut of butchered or flayed meat came from. Same fields as
/// <see cref="BadBloodTrophyOriginComponent"/>. Sliced cutlets do not keep it.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodMeatOriginComponent : Component
{
    [DataField]
    public EntityUid Body;

    [DataField]
    public bool WasPlayer;

    [DataField]
    public bool IsYautja;

    [DataField]
    public bool IsSynth;

    [DataField]
    public bool IsXeno;

    [DataField]
    public bool IsLarva;

    [DataField]
    public bool WasKill;
}
