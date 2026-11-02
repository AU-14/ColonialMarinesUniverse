// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Smelting;

// A press that runs on wood instead of the grid. A shape (the mold) goes into its mold slot, one or two ingots into its
// input slots, planks into its fuel slot, and the pressed piece comes out in the output slots. With one ingot only that
// one is pressed; with two they are combined into a single piece, if there is a recipe for the two of them.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SolidFuelPressComponent : Component
{
    public const string MoldSlotId = "mold";
    public const string FuelSlotId = "fuel";
    public static readonly string[] InputSlotIds = { "input1", "input2" };
    public static readonly string[] OutputSlotIds = { "output1", "output2", "output3", "output4" };

    // The stack that burns in it.
    [DataField]
    public ProtoId<StackPrototype> FuelStack = "RMCWood";

    // How long a single plank keeps it going, in seconds of work.
    [DataField]
    public float PlankBurnSeconds = 40f;

    // A steam press burns nothing: it runs while it has steam, and uses this many moles of it every second of work.
    [DataField]
    public float SteamPerSecond;

    // How much of the water it keeps for the pieces that need liquids (the steam is counted on its own, in its own port).
    [DataField]
    public float StorageLimit = 100f;

    [DataField, AutoNetworkedField]
    public bool Working;

    // When the piece in the press is done, and how long its pressing takes, for the progress bar.
    [DataField, AutoNetworkedField]
    public TimeSpan? WorkEndsAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? WorkDuration;

    // What is left of the plank that is burning, in seconds of work.
    [ViewVariables]
    public float FuelLeft;

    // What is left of a pressing that was stopped half way (no fuel, no room), and which recipe it was.
    [ViewVariables]
    public TimeSpan? WorkRemaining;

    [ViewVariables]
    public string? ActiveRecipe;
}

// Key of the solid fuel press window.
[Serializable, NetSerializable]
public enum SolidFuelPressUiKey : byte
{
    Key,
}

// The shape of a mold: the press looks at it to know what to make of the ingots.
[RegisterComponent, NetworkedComponent]
public sealed partial class PressMoldComponent : Component
{
    [DataField(required: true)]
    public string Shape = string.Empty;
}

// What the press makes of one ingot, or of two of them, in a mold of a given shape. A recipe with no shape is pressed with no mold at all.
[Prototype("cmuPressRecipe")]
public sealed partial class PressRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // The shape of the mold the recipe needs; empty when it needs none (the slot of the mold is left empty).
    [DataField]
    public string Shape = string.Empty;

    // One or two stacks, each with how many of it is used up.
    [DataField(required: true)]
    public Dictionary<ProtoId<StackPrototype>, int> Inputs = new();

    [DataField(required: true)]
    public ProtoId<StackPrototype> Result = default!;

    [DataField]
    public int ResultAmount = 1;

    // Seconds of work.
    [DataField]
    public float Duration = 6f;

    // Liquids the piece needs, taken out of the press's own storage when it is done: the fluid and how much of it.
    [DataField]
    public Dictionary<string, float> Fluids = new();
}
