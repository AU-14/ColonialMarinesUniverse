using Content.Shared.Physics;

namespace Content.Server.CMU14.Weapons.Gauss;

/// <summary>
///     The gauss gun's hitscan raycast, used on its shots in place of <c>HitscanBasicRaycast</c>. It can hit
///     someone lying down that the shooter aimed at, and with <see cref="MaxBounces"/> ricochets off walls
///     and other static structures. Mobs stop the beam.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGaussRaycastComponent : Component
{
    /// <summary>How many times the beam can bounce off walls.</summary>
    [DataField]
    public int MaxBounces;

    /// <summary>How far each leg of the beam can travel.</summary>
    [DataField]
    public float MaxDistance = 20f;

    [DataField]
    public CollisionGroup CollisionMask = CollisionGroup.Opaque;
}

/// <summary>
///     Hitscan shots with this have a chance to set what they hit lightly on fire.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGaussIgniteComponent : Component
{
    [DataField]
    public float Chance = 0.5f;

    [DataField]
    public int Intensity = 5;

    [DataField]
    public int Duration = 6;
}
