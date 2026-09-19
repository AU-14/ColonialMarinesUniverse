using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Biomorph;

/// <summary>
///     Grants a mimic the ability to assimilate an incapacitated humanoid via doafter.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BiomorphAssimilateComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(8);
}
