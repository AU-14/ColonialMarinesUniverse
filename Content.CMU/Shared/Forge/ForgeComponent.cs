// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Forge;

// The crucible of the forge: what it can melt (ingredients), how much molten metal it holds, and what is loaded in it.
[RegisterComponent, NetworkedComponent]
public sealed partial class ForgeComponent : Component
{
    [DataField]
    public string Solution = "melt";

    [DataField]
    public FixedPoint2 Capacity = FixedPoint2.New(150);

    [DataField]
    public List<ForgeIngredient> Ingredients = new();

    // What was put in since the crucible was last empty, by stack: so that it can be taken back out before it melts.
    [ViewVariables]
    public Dictionary<ProtoId<StackPrototype>, int> Loaded = new();
}

// What a crucible takes: a stack, the reagent it melts into, and how much per unit.
[DataDefinition]
public sealed partial class ForgeIngredient
{
    [DataField(required: true)]
    public ProtoId<StackPrototype> Input;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public FixedPoint2 ReagentPerUnit = FixedPoint2.New(10);
}
