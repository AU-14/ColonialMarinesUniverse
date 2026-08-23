namespace Content.Shared._CMU14.Weapons.Ranged;

/// <summary>
/// Marks a spent casing ejected from a gun, so the server can strip its physics
/// and despawn it later without a per-entity timer.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSpentCasingComponent : Component
{
    /// <summary>When the casing was ejected; drives both the physics strip and the despawn.</summary>
    public TimeSpan EjectedAt;
}
