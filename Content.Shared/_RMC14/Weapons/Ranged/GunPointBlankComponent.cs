using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMGunSystem))]
public sealed partial class GunPointBlankComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 ProneDamageMult = 1.2;

    [DataField, AutoNetworkedField]
    public float Range = 1.5f;

    /// <summary>
    /// CMU14: multiplier applied to the accuracy of every projectile fired point blank, so a spread weapon
    /// still puts most of its pellets into an adjacent target instead of scattering them past it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 AccuracyMultiplier = 2;
}
