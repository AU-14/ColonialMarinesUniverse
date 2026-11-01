using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public TimeSpan ReturnFireMemory = TimeSpan.FromSeconds(2.5);
    [DataField] public int ReturnFireRoundLimit = 6;
    public EntityCoordinates? ReturnFirePoint;
    public TimeSpan ReturnFireUntil;
    public TimeSpan ReturnFireWindowEnd;
    public int ReturnFireRounds;
    public bool FiringAtSuppression;
    public int SuppressiveShots;
    public TimeSpan WeaponRaisedUntil;
}
