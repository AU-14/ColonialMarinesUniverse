using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Entrenching;

/// <summary>
///     Marks a HESCO under construction that can be packed with dirt using an entrenching tool.
///     Every completed fill tick adds <see cref="ProgressPerTick"/> to the shared <see cref="Progress"/>
///     regardless of which player performed it, so several marines filling it at once complete it faster.
///     Once <see cref="Progress"/> reaches <see cref="Required"/> the entity is replaced by <see cref="NextStage"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(BarricadeSystem))]
public sealed partial class HescoFillableComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId NextStage;

    [DataField, AutoNetworkedField]
    public float Progress;

    [DataField, AutoNetworkedField]
    public float Required = 5;

    [DataField, AutoNetworkedField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public float ProgressPerTick = 1;
}
