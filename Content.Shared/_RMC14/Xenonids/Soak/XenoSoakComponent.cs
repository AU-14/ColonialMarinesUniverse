using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Soak;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoSoakComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = FixedPoint2.New(20);

    /// <summary>
    ///     Override for the damage goal. If null, uses the default from XenoSoakingDamageComponent.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? DamageGoal;

    /// <summary>
    ///     Override for the heal amount. If null, uses the default from XenoSoakingDamageComponent.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2? Heal;
}
