using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Entrenching;

/// <summary>
///     Placed on the ground to create an unfilled HESCO basket (<see cref="Builds"/>), anchored and facing
///     whichever direction the user was facing when they used it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(BarricadeSystem))]
public sealed partial class HescoKitComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Builds = "AU14HescoUnfilled1";

    [DataField, AutoNetworkedField]
    public TimeSpan BuildDelay = TimeSpan.FromSeconds(2);
}
