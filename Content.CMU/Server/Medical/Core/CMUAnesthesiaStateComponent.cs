namespace Content.Server.CMU14.Medical.Core;

[RegisterComponent]
public sealed partial class CMUAnesthesiaStateComponent : Component
{
    public EntityUid GasTank;

    public EntityUid? Drowsiness;

    public EntityUid? ForcedSleep;

    public bool Induced;

    /// <summary>Only wake sleep first introduced by this anesthesia session.</summary>
    public bool OwnsSleep;
}
