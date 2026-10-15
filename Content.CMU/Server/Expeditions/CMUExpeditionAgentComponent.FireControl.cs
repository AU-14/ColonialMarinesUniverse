namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public int FireControlVolley;
    public TimeSpan FireControlDuration;
    public TimeSpan? FireControlRecovery;
    public bool SustainedFire;
    public EntityUid? FireControlWeapon;
    public EntityUid? FireControlTarget;
    public EntityUid? FireControlObservedTarget;
    public EntityUid? LastFireControlShotTarget;
    public bool FireControlTargetVisible;
    public TimeSpan FireControlLastVisible;
    public TimeSpan FireControlPeekUntil;
    public TimeSpan NextFireControl;
    public TimeSpan FireControlBurstStarted;
    public TimeSpan FireControlMovingBurstStarted;
}
