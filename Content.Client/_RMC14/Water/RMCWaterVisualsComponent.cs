using System.Numerics;

namespace Content.Client._RMC14.Water;

/// <summary>Client-only presentation state, present only while a mob is on or leaving water.</summary>
[RegisterComponent]
public sealed partial class RMCWaterVisualsComponent : Component
{
    public EntityUid? Water;
    public EntityUid? Splash;
    public float Depth;
    public float TargetDepth;
    public bool Immersed;
    public Vector2 BaseOffset;
    public Vector2 LastOffset;
    public TimeSpan MovingUntil;
    public TimeSpan NextSurfaceCheck;
    public string? SplashState;
    public int SplashSize;
}
