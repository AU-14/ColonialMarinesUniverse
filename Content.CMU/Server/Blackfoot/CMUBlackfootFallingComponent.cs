namespace Content.Server.CMU14.Blackfoot;

[RegisterComponent, UnsavedComponent]
public sealed partial class CMUBlackfootFallingComponent : Component
{
    public TimeSpan CrashAt;
    public float AngularVelocityDegrees;
}
