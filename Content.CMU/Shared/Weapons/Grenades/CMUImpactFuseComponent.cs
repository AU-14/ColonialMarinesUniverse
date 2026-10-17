using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.Grenades;

/// <summary>
/// Launcher-fired grenade that goes off on impact: the first mob it reaches, a wall, or the spot it was aimed at.
/// Pair with IgnoreArc so it flies straight like a bullet instead of being lobbed over targets.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CMUImpactFuseComponent : Component
{
    /// <summary>How close a mob has to be to the grenade in flight to set it off, in tiles.</summary>
    [DataField]
    public float HitRadius = 0.45f;

    /// <summary>Mobs are ignored for this long after firing, so the shooter doesn't set it off.</summary>
    [DataField]
    public TimeSpan ArmDelay = TimeSpan.FromSeconds(0.1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? LaunchedAt;

    [DataField]
    public bool Detonated;
}
