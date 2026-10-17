using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components;

/// <summary>
/// Allows an entity to instantly transfer liquids by interacting with objects that have solutions.
/// Retained for fork hyposprays; upstream injectors use <see cref="InjectorComponent"/> instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HyposprayComponent : Component
{
    /// <summary>
    /// Solution used by the hypospray for injections.
    /// </summary>
    [DataField]
    public string SolutionName = "hypospray";

    /// <summary>
    /// Amount transferred per use.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(5);

    /// <summary>
    /// Sound played when injecting.
    /// </summary>
    [DataField]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    /// <summary>
    /// Whether this hypospray may inject non-mob entities.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public bool OnlyAffectsMobs;

    /// <summary>
    /// Whether mob-only mode can still draw from solution containers.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanContainerDraw = true;

    /// <summary>
    /// Whether this device can only inject and cannot draw.
    /// </summary>
    [DataField]
    public bool InjectOnly;
}
