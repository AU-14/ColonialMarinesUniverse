namespace Content.Server.CMU14.Fighter;

[RegisterComponent, UnsavedComponent]
public sealed partial class CMUManpadLowAltitudeAimingComponent : Component
{
    public EntityUid? Target;
    public TimeSpan NextScan;
    public TimeSpan LockStartedAt;
    public TimeSpan LockCompleteAt;
    public TimeSpan NextBeep;
    public TimeSpan NextAcquire;
}
