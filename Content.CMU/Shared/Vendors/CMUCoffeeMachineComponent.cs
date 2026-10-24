using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Vendors;

/// <summary>
///     A coffee machine: put a cup or mug in it, it brews a fresh cup of coffee into it, then take it back out.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CMUCoffeeMachineComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "RMCCoffee";

    /// <summary>The most it pours into one container per brew.</summary>
    [DataField]
    public FixedPoint2 Amount = 30;

    /// <summary>How long a brew takes if there's no <see cref="BrewSound"/>; otherwise it takes as long as the sound.</summary>
    [DataField]
    public TimeSpan BrewTime = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier? BrewSound = new SoundPathSpecifier("/Audio/Machines/machine_vend_hot_drink.ogg");

    [DataField]
    public string ContainerId = "cmu_coffee_cup";

    /// <summary>The machine's sprite state with each kind of cup sitting in it.</summary>
    [DataField]
    public Dictionary<EntProtoId, string> CupStates = new();

    /// <summary>The sprite state for any other cup.</summary>
    [DataField]
    public string GenericCupState = "coffee_cup_generic";

    [DataField]
    public string EmptyState = "coffee_empty_on";

    /// <summary>When the cup in the machine finishes brewing, or null if nothing is brewing.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? BrewEnd;

    /// <summary>Who put the cup in, so they can be told when it's ready.</summary>
    [DataField]
    public EntityUid? Brewer;
}

[Serializable, NetSerializable]
public enum CMUCoffeeMachineVisuals : byte
{
    State,
}
