namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public bool MovingFire;
    public int MovingShotsFired;
    public int TotalMovingShots;
    public TimeSpan MovingBurstEnd;
    public TimeSpan NextMovingBurst;
    public TimeSpan ContactMoveUntil;
    public TimeSpan ImmediateFireUntil;
}
