using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination;

/// <summary>
///     Grants a mimic the ability to assimilate an incapacitated humanoid via doafter.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationAssimilateComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan DoAfter = TimeSpan.FromSeconds(8);
}
