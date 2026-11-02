// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Electric;

// What a machine of the electric age makes: the stacks and liquids it uses up when it starts, and the stacks and liquids it gives
// when it is done. A recipe with a circuit is made only while that circuit is set on the machine.
[Prototype("cmuMachineRecipe")]
public sealed partial class MachineRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // The kind of machine that makes it (RecipeMachineComponent.Kind).
    [DataField(required: true)]
    public string Machine = string.Empty;

    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> Inputs = new();

    // Stacks that have to be in the slots but are not used up: the lens of the laser engraver, for one.
    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> Catalysts = new();

    // Things that are not stacks, which the machine takes whole: only a machine whose slots take any item (the recycler) has them.
    [DataField]
    public Dictionary<EntProtoId, int> ItemInputs = new();

    [DataField]
    public Dictionary<ProtoId<StackPrototype>, int> Outputs = new();

    // Things that are not stacks: a power cell, a light bulb, a circuit module. Each is an entity of its own and takes a slot of its own.
    [DataField]
    public Dictionary<EntProtoId, int> ItemOutputs = new();

    // Liquids are reagents: how much of each is taken out of the tank and put into it.
    [DataField]
    public Dictionary<string, float> FluidInputs = new();

    [DataField]
    public Dictionary<string, float> FluidOutputs = new();

    // The circuit that has to be set; none means the recipe is made whatever is set.
    [DataField]
    public int? Circuit;

    // Seconds of work at the speed of 1.
    [DataField]
    public float Duration = 5f;
}
