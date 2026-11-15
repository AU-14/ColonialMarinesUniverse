// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Grinding;

// A recipe of a grinder: the stack it takes and the stack (and extras) it makes.
[Prototype("cmuGrinderRecipe")]
public sealed partial class GrinderRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<StackPrototype> Input = default!;

    [DataField]
    public int InputAmount = 1;

    [DataField(required: true)]
    public ProtoId<StackPrototype> Result = default!;

    [DataField]
    public int ResultAmount = 1;

    // What else comes out of the same grind, each in a slot of its own: the stone dust of an ore, for one.
    [DataField]
    public List<GrinderRecipeExtra> Extras = new();
}

// An extra product of a grinder recipe.
[DataDefinition]
public sealed partial class GrinderRecipeExtra
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> Result = default!;

    [DataField]
    public int Amount = 1;
}
