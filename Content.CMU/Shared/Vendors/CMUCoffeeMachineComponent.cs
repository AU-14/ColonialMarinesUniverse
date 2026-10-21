using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vendors;

/// <summary>
///     A coffee machine: use a cup, mug or other drink container on it and it brews a fresh cup of coffee into it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCoffeeMachineComponent : Component
{
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "RMCCoffee";

    /// <summary>The most it pours into one container per brew.</summary>
    [DataField]
    public FixedPoint2 Amount = 30;

    [DataField]
    public TimeSpan BrewTime = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier? BrewSound = new SoundPathSpecifier("/Audio/Machines/machine_vend_hot_drink.ogg");
}

[Serializable, NetSerializable]
public sealed partial class CMUCoffeeBrewDoAfterEvent : SimpleDoAfterEvent;
