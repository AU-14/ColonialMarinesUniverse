using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Entrenching;

/// <summary>
///     Put on every HESCO build stage. Selecting the "Disassemble" verb arms <see cref="Disassembling"/>; while
///     armed, using an entrenching tool on the entity undoes the step that built it - progressively (mirroring
///     <see cref="HescoFillableComponent"/>) unless <see cref="Instant"/> (mirroring <see cref="HescoRaisableComponent"/>) -
///     and transitions to <see cref="PreviousStage"/>, or deletes the entity and hands back <see cref="ReturnPrototype"/>
///     if this is the first stage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HescoDisassemblableComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? PreviousStage;

    [DataField, AutoNetworkedField]
    public EntProtoId? ReturnPrototype = "AU14HescoKit";

    [DataField, AutoNetworkedField]
    public bool Instant;

    [DataField, AutoNetworkedField]
    public bool Disassembling;

    [DataField, AutoNetworkedField]
    public float Progress;

    [DataField, AutoNetworkedField]
    public float Required = 5;

    [DataField, AutoNetworkedField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public float ProgressPerTick = 1;
}
