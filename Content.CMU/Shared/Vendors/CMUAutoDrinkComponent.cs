using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vendors;

/// <summary>
///     A NutriCo AutoDrink: a mess drink dispenser that pours drinks, made on the spot from water and concentrate, into
///     whatever container is in its bay. It can also drop a fresh disposable cup into an empty bay.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAutoDrinkComponent : Component
{
    public const string BaySlotId = "autodrink_bay";

    [DataField]
    public ItemSlot Bay = new();

    /// <summary>What it can pour, in menu order.</summary>
    [DataField(required: true)]
    public List<CMUAutoDrinkEntry> Drinks = new();

    /// <summary>How much each pour can be, in units.</summary>
    [DataField]
    public List<FixedPoint2> PourAmounts = new() { 5, 10, 15, 30 };

    [DataField]
    public EntProtoId Cup = "CMUNutriCoCup";

    /// <summary>The shortest time between cups, so the bay can't be spammed full of trash.</summary>
    [DataField]
    public TimeSpan CupCooldown = TimeSpan.FromSeconds(2);

    [DataField]
    public SoundSpecifier PourSound = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");

    [DataField]
    public SoundSpecifier CupSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    [ViewVariables]
    public TimeSpan NextCup;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUAutoDrinkEntry
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>The name on the button; the reagent's own name if unset.</summary>
    [DataField]
    public LocId? Name;

    /// <summary>A short line under the name, like "hot" or "electrolyte".</summary>
    [DataField]
    public LocId? Note;

    /// <summary>An extra to add to a drink, like ice, listed apart from the drinks themselves.</summary>
    [DataField]
    public bool Extra;
}

[Serializable, NetSerializable]
public enum CMUAutoDrinkUiKey : byte
{
    Key,
}

/// <summary>One reagent in the container in the bay.</summary>
[Serializable, NetSerializable]
public readonly record struct CMUAutoDrinkContent(string Name, FixedPoint2 Quantity, Color Color);

[Serializable, NetSerializable]
public sealed class CMUAutoDrinkBuiState(
    string? container,
    FixedPoint2 volume,
    FixedPoint2 maxVolume,
    List<CMUAutoDrinkContent> contents) : BoundUserInterfaceState
{
    /// <summary>The name of whatever's in the bay, or null if it's empty.</summary>
    public readonly string? Container = container;

    public readonly FixedPoint2 Volume = volume;
    public readonly FixedPoint2 MaxVolume = maxVolume;
    public readonly List<CMUAutoDrinkContent> Contents = contents;
}

[Serializable, NetSerializable]
public sealed class CMUAutoDrinkPourBuiMsg(ProtoId<ReagentPrototype> drink, FixedPoint2 amount) : BoundUserInterfaceMessage
{
    /// <summary>The drink, by reagent rather than menu position, so a menu change can't pour the wrong one.</summary>
    public readonly ProtoId<ReagentPrototype> Drink = drink;
    public readonly FixedPoint2 Amount = amount;
}

[Serializable, NetSerializable]
public sealed class CMUAutoDrinkCupBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUAutoDrinkEjectBuiMsg : BoundUserInterfaceMessage;
