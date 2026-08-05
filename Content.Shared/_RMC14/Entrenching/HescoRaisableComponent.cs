using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Entrenching;

/// <summary>
///     Marks a completed short HESCO that can be raised into a taller, unfilled frame (<see cref="NextStage"/>)
///     with a single quick entrenching tool use, ahead of being filled in again.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(BarricadeSystem))]
public sealed partial class HescoRaisableComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId NextStage;

    [DataField, AutoNetworkedField]
    public TimeSpan RaiseDelay = TimeSpan.FromSeconds(3);
}
