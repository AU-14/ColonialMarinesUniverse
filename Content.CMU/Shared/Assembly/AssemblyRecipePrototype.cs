// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Assembly;

// A recipe of an assembly machine: which material goes into which slot, what it makes, how many, how long and how much energy it takes.
[Prototype("cmuAssemblyRecipe")]
public sealed partial class AssemblyRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<int, AssemblySlotRequirement> Layout = new();

    [DataField(required: true)]
    public EntProtoId Result = default!;

    [DataField]
    public int ResultAmount = 1;

    [DataField]
    public TimeSpan AssembleTime = TimeSpan.FromSeconds(5);

    [DataField]
    public float EnergyCost = 300;
}
