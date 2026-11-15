// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Smelting;

// A recipe of alloying: which stacks go in, the lowest temperature, how long it takes and what comes out.
[Prototype("cmuAlloyRecipe")]
public sealed partial class AlloyRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<ProtoId<StackPrototype>, int> Materials = new();

    [DataField]
    public float MinTemperature = 500f;

    [DataField(required: true)]
    public ProtoId<StackPrototype> Result = default!;

    [DataField]
    public int ResultAmount = 1;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    [DataField]
    public LocId? Name;
}
