// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Steam;

// What the steam smelter makes: ore (or other material) in the material slots and a mold of the right shape in the mold slot
// turn into ingots, leaving slag. The smelter has no recipes of its own: it makes only what is written down here.
[Prototype("cmuSteamSmeltRecipe")]
public sealed partial class SteamSmeltRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    // The shape of the mold that has to be in the mold slot.
    [DataField(required: true)]
    public string Shape = string.Empty;

    // Stacks used up and how much of each.
    [DataField(required: true)]
    public Dictionary<ProtoId<StackPrototype>, int> Inputs = new();

    [DataField(required: true)]
    public ProtoId<StackPrototype> Result = default!;

    [DataField]
    public int ResultAmount = 1;

    // Seconds of work at full steam.
    [DataField]
    public float Duration = 10f;

    // Slag left in the smelter by the operation.
    [DataField]
    public float Slag = 15f;
}
